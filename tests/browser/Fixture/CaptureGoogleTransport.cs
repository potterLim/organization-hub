using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.BrowserFixture;

public sealed class CaptureGoogleTransport : IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
{
    private static readonly Dictionary<string, JsonObject> sItems = new Dictionary<string, JsonObject>();
    private static int sVersion = 1;
    private static readonly object sGate = new object();
    public static void Reset()
    {
        lock (sGate)
        {
            sItems.Clear();
            sVersion = 1;
        }
    }
    public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken token) => Task.FromResult<IConfigurableHttpClientInitializer>(this);
    public void Initialize(ConfigurableHttpClient client)
    {
        client.MessageHandler.InnerHandler = new Handler();
        client.MessageHandler.NumTries = 1;
    }
    private sealed class Handler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string path = request.RequestUri!.AbsolutePath;
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri.Query);
            if (CaptureState.Mode == "loading")
            {
                await Task.Delay(3500, token);
            }

            if (CaptureState.Mode == "error")
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":{\"code\":400,\"message\":\"Service temporarily unavailable\"}}", Encoding.UTF8, "application/json") };
            }

            if ((CaptureState.Mode == "write-reject" || (CaptureState.Mode == "task-reject" && path.Contains("/tasks/"))) && request.Method == HttpMethod.Patch)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":{\"code\":400,\"message\":\"Definite rejected edit\"}}", Encoding.UTF8, "application/json") };
            }

            if (path.Contains("calendarList"))
            {
                return response(new
                {
                    items = new[] { new { id = "qa-calendar", summary = "조직 공용 캘린더", accessRole = "owner", primary = true }, new { id = "team-calendar", summary = "경영지원팀 일정", accessRole = "writer", primary = false }, new { id = "planning-calendar", summary = "서비스기획팀 프로젝트", accessRole = "reader", primary = false } }
                });
            }

            if (path.EndsWith("/lists"))
            {
                return response(new
                {
                    items = new[] { new { id = "qa-tasks", title = "조직 공용 할 일" }, new { id = "team-tasks", title = "경영지원팀 실행 과제" }, new { id = "planning-tasks", title = "서비스 개선 과제" } }
                });
            }

            if (path.StartsWith("/drive/"))
            {
                IReadOnlyList<DriveFileItem> fs = await CaptureState.Drive.ListFilesAsync(DriveFolderId.CreateRequired("qa-root"), token);
                return response(new
                {
                    files = fs.Where(f => f.IsFolder).Select(f => new { id = f.Id, name = f.Name, mimeType = f.MimeType })
                });
            }
            bool calendar = path.Contains("/calendar/");
            bool list = path.EndsWith(calendar ? "/events" : "/tasks");
            if (calendar && list && CaptureState.Mode == "calendar-layout")
            {
                if (path.Contains("team-calendar"))
                {
                    return response(new
                    {
                        items = Array.Empty<object>()
                    });
                }

                int[] counts = { 1, 12, 99, 100, 999 };
                var events = counts.SelectMany((count, index) => Enumerable.Range(0, count).Select(n => new { id = $"layout-{index}-{n}", etag = "\"layout1\"", summary = "긴 일정 제목 · " + new string('A', 160) + " · 조직 업무 검토", location = new string('B', 120), start = new { dateTime = $"2026-10-{19 + index:00}T09:00:00+09:00" }, end = new { dateTime = $"2026-10-{19 + index:00}T10:30:00+09:00" } })).ToArray();
                return response(new
                {
                    items = events
                });
            }
            string id = list ? (calendar ? "event1" : "task1") : path.EndsWith("/move") ? path.Split('/')[^2] : path.Split('/').Last();
            string body = "{}";
            if (request.Content != null)
            {
                byte[] bytes = await request.Content.ReadAsByteArrayAsync(token);
                if (bytes.Length > 1 && bytes[0] == 31 && bytes[1] == 139)
                {
                    using (GZipStream gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
                    using (StreamReader reader = new StreamReader(gzip))
                    {
                        body = await reader.ReadToEndAsync(token);
                    }
                }
                else
                {
                    body = Encoding.UTF8.GetString(bytes);
                }
            }
            string scope = path.Substring(0, path.LastIndexOf(calendar ? "/events" : "/tasks", StringComparison.Ordinal));
            lock (sGate)
            {
                JsonObject item = getItem(calendar, id, scope);
                if (request.Method == HttpMethod.Get && !list && item["deleted"]?.GetValue<bool>() == true)
                {
                    return new HttpResponseMessage(HttpStatusCode.Gone);
                }

                if (path.EndsWith("/move"))
                {
                    string prefix = id.StartsWith("team-") ? "team-" : "";
                    List<JsonObject> ordered = Enumerable.Range(1, 8).Select(n => getItem(false, prefix + "task" + n, scope)).OrderBy(x => x["position"]!.GetValue<string>()).ToList();
                    ordered.Remove(item);
                    string previous = query["previous"].ToString();
                    int index = ordered.FindIndex(x => x["id"]!.GetValue<string>() == previous);
                    ordered.Insert(index + 1, item);
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        ordered[i]["position"] = (i + 1).ToString("0000");
                    }

                    item["etag"] = "\"capture" + (++sVersion) + "\"";
                    return response(item);
                }

                if (request.Method == HttpMethod.Delete)
                {
                    if (request.Headers.IfMatch.Count > 0 && request.Headers.IfMatch.ToString() != item["etag"]!.GetValue<string>())
                    {
                        return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
                    }

                    item["deleted"] = true;
                    if (CaptureState.Mode == "delete-lost")
                    {
                        throw new HttpRequestException("Response lost after deletion");
                    }

                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
                if (request.Method == HttpMethod.Patch || request.Method == HttpMethod.Put || request.Method == HttpMethod.Post)
                {
                    if (request.Headers.IfMatch.Count > 0 && request.Headers.IfMatch.ToString() != item["etag"]!.GetValue<string>())
                    {
                        return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
                    }

                    JsonObject change = JsonNode.Parse(body)!.AsObject();
                    foreach (KeyValuePair<string, JsonNode?> pair in change)
                    {
                        item[pair.Key] = pair.Value?.DeepClone();
                    }

                    if (!calendar && item["due"] != null)
                    {
                        item["due"] = item["due"]!.GetValue<string>().Substring(0, 10) + "T00:00:00Z";
                    }

                    item["etag"] = "\"capture" + (++sVersion) + "\"";
                    if (item["status"]?.GetValue<string>() == "completed")
                    {
                        item["completed"] = "2026-10-19T02:30:00Z";
                    }
                }
                if (request.Method == HttpMethod.Get && list)
                {
                    return response(new
                    {
                        items = CaptureState.Mode == "empty" ? Array.Empty<JsonObject>() : Enumerable.Range(1, 8).Select(n => getItem(calendar, ((path.Contains("team-calendar") || path.Contains("team-tasks")) ? "team-" : "") + (calendar ? "event" : "task") + n, scope)).Where(x => x["deleted"]?.GetValue<bool>() != true).ToArray()
                    });
                }
                return response(item);
            }
        }
        private static JsonObject getItem(bool calendar, string id, string scope)
        {
            string key = scope + ":" + id;
            JsonObject? foundOrNull;
            if (sItems.TryGetValue(key, out foundOrNull))
            {
                return foundOrNull;
            }

            int number;
            int n = int.TryParse(new string(id.Where(char.IsDigit).ToArray()), out number) ? number : 1;
            int day = 18 + n;
            string[] eventNames = { "주간 운영 점검 회의", "4분기 실행 계획 공유", "신규 구성원 온보딩", "서비스 개선안 중간 검토", "예산 집행 현황 확인", "부서 간 협업 워크숍", "협력사 정기 미팅", "월간 성과 공유 및 다음 달 목표 수립" };
            string[] taskNames = { "분기 운영 계획 최종 검토", "교육 안내 자료 배포", "구매 예산 산출 근거 보완", "신규 입사자 계정 및 권한 준비", "협력사 미팅 안건 정리", "회의록 정리 및 공유", "교육 참석자 명단 확인", "서비스 개선 제안서 제출" };
            JsonObject value = JsonNode.Parse(calendar ? JsonSerializer.Serialize(new
            {
                id,
                etag = "\"capture1\"",
                summary = (id.StartsWith("team-") ? "팀 협업 · " : "") + eventNames[(n - 1) % 8],
                description = "담당자와 진행 상황을 확인하고 필요한 자료를 미리 공유해 주세요.\n참석 대상: 경영지원팀 및 관련 부서",
                location = n % 2 == 0 ? "온라인 회의 · Google Meet" : "본사 3층 회의실 A",
                start = new
                {
                    dateTime = $"2026-10-{day:00}T{(n % 2 == 0 ? 14 : 9):00}:00:00+09:00"
                },
                end = new
                {
                    dateTime = $"2026-10-{day:00}T{(n % 2 == 0 ? 15 : 10):00}:30:00+09:00"
                }
            }) : JsonSerializer.Serialize(new
            {
                id,
                etag = "\"capture1\"",
                title = (id.StartsWith("team-") ? "팀 실행 · " : "") + taskNames[(n - 1) % 8],
                status = n >= 7 ? "completed" : "needsAction",
                completed = n >= 7 ? "2026-10-18T03:15:00Z" : null,
                position = n.ToString("0000"),
                due = n < 7 ? $"2026-10-{day:00}T00:00:00Z" : null,
                updated = "2026-10-19T01:00:00Z"
            }))!.AsObject();
            sItems[key] = value;
            return value;
        }
        private static HttpResponseMessage response(object value) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
}
