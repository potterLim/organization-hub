using System;
using System.Collections.Generic;
using System.IO;
using OrganizationHub.Web.Tests.Fakes;

namespace OrganizationHub.BrowserFixture;

public static class CaptureState
{
    public static string Mode { get; set; } = "normal";
    public static byte[] DocumentContent { get; } = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "sample.pdf"));
    public static FakeDriveService Drive { get; set; } = CreateDrive();
    public static FakeDriveService CreateDrive()
    {
        FakeDriveService drive = new FakeDriveService();
        drive.AddFolder("qa-root", "조직 공용 드라이브");
        Dictionary<string, string> folders = new Dictionary<string, string> { { "qa-team", "경영지원팀 자료" }, { "qa-approved", "경영지원팀 승인본" }, { "qa-common", "공통 자료" }, { "qa-public", "외부 공유 자료" }, { "qa-responsible", "책임자 자료" }, { "qa-work", "업무 지원 자료" }, { "qa-planning", "서비스기획팀 자료" } };
        foreach (KeyValuePair<string, string> folder in folders)
        {
            drive.AddFolder(folder.Key, folder.Value, "qa-root");
            drive.AddFolder(folder.Key + "-sub", "2026년 4분기 운영 자료", folder.Key, "qa-root");
            string[] names = { "2026-10-19_경영지원팀_분기운영계획.pdf", "2026-10-18_기획팀_서비스개선제안.pdf", "2026-10-17_공통_신규입사자안내.pdf", "2026-10-16_운영팀_주간회의록.pdf", "2026-10-15_경영지원팀_구매예산산출내역.xlsx", "2026-10-14_기획팀_고객경험개선과제_최종검토본.pdf" };
            for (int i = 0; i < names.Length; i++)
            {
                string id = folder.Key + "-file" + (i + 1);
                drive.AddFile(id, names[i], i == 4 ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf", folder.Key, "qa-root");
                drive.SetFileContent(id, DocumentContent);
            }
            drive.AddFile(folder.Key + "-sub-file", "2026-10_세부실행계획.pdf", "application/pdf", folder.Key + "-sub", folder.Key, "qa-root");
            drive.SetFileContent(folder.Key + "-sub-file", DocumentContent);
        }
        drive.AddFile("sample-pdf1", "2026-10-19_경영지원팀_분기운영계획.pdf", "application/pdf", "qa-team", "qa-root");
        drive.SetFileContent("sample-pdf1", DocumentContent);
        drive.AddFile("sample-pdf2", "조직 업무 가이드.pdf", "application/pdf", "qa-work", "qa-root");
        drive.SetFileContent("sample-pdf2", DocumentContent);
        for (int i = 1; i < 34; i++)
        {
            drive.AddFile("approved-pdf" + i, "승인완료_운영계획_" + i + ".pdf", "application/pdf", "qa-approved", "qa-root");
            drive.SetFileContent("approved-pdf" + i, DocumentContent);
        }
        return drive;
    }
}
