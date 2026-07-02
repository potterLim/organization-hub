using System;
using OrganizationHub.Web.Domain;
using Xunit;

namespace OrganizationHub.Web.Tests.Domain;

public sealed class TodoDueAtUtcTests
{
    [Fact]
    public void CreateOptionalUtcAcceptsUtcDateTime()
    {
        DateTime dueAtUtc = new DateTime(2026, 6, 27, 9, 30, 0, DateTimeKind.Utc);

        TodoDueAtUtc todoDueAtUtc = TodoDueAtUtc.CreateOptionalUtc(dueAtUtc);

        Assert.True(todoDueAtUtc.HasValue);
        Assert.Equal(dueAtUtc, todoDueAtUtc.GetOptionalValueOrNull());
    }

    [Fact]
    public void CreateRequiredUtcRejectsLocalDateTime()
    {
        DateTime localDueAt = new DateTime(2026, 6, 27, 18, 30, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => TodoDueAtUtc.CreateRequiredUtc(localDueAt));
    }

    [Fact]
    public void CreateRequiredFromStoredValueNormalizesUnspecifiedDateTime()
    {
        DateTime storedDueAt = new DateTime(2026, 6, 27, 9, 30, 0, DateTimeKind.Unspecified);
        DateTime expectedDueAtUtc = DateTime.SpecifyKind(storedDueAt, DateTimeKind.Utc);

        TodoDueAtUtc todoDueAtUtc = TodoDueAtUtc.CreateRequiredFromStoredValue(storedDueAt);

        Assert.Equal(expectedDueAtUtc, todoDueAtUtc.GetRequiredValue());
    }
}
