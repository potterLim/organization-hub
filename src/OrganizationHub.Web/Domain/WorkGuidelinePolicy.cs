using System;

namespace OrganizationHub.Web.Domain;

public static class WorkGuidelinePolicy
{
    public const int MIN_SECTION_COUNT = 0;
    public const int MAX_SECTION_COUNT = 9;
    public const int SORT_ORDER_STEP = 10;

    private const int EMPTY_SECTION_COUNT = 0;
    private const int THREE_CARD_SECTION_COUNT = 3;
    private const int FIVE_CARD_SECTION_COUNT = 5;

    public static EWorkGuidelineGridLayout GetDisplayLayout(int sectionCount)
    {
        if (IsValidSectionCount(sectionCount) == false)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sectionCount),
                "Work guideline section count must be between " + MIN_SECTION_COUNT + " and " + MAX_SECTION_COUNT + ".");
        }

        if (sectionCount == EMPTY_SECTION_COUNT)
        {
            return EWorkGuidelineGridLayout.Empty;
        }

        if (sectionCount == THREE_CARD_SECTION_COUNT || sectionCount >= FIVE_CARD_SECTION_COUNT)
        {
            return EWorkGuidelineGridLayout.ThreeColumns;
        }

        return EWorkGuidelineGridLayout.TwoColumns;
    }

    public static int GetSortOrderForPosition(int position)
    {
        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Work guideline position must be greater than zero.");
        }

        return position * SORT_ORDER_STEP;
    }

    public static bool IsValidSectionCount(int sectionCount)
    {
        return sectionCount >= MIN_SECTION_COUNT && sectionCount <= MAX_SECTION_COUNT;
    }
}
