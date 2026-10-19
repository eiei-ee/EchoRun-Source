/// <summary>Authored district identities and varied local signs, baked by the architecture installer.</summary>
public static class StackedCityWallCatalog
{
    public const int DistrictCount = 4;
    public const int NoticeCount = 12;

    public static readonly string[] ModelNames =
    {
        "DistrictSignXixia", "DistrictSignYunting", "DistrictSignQinglan", "DistrictSignZhexiang",
        "CityNoticeNoodle", "CityNoticeBookshop", "CityNoticeTailor", "CityNoticePost",
        "CityNoticeGarden", "CityNoticeNightBus", "CityNoticeWind", "CityNoticeNeighbours",
        "CityNoticeYesterday", "CityNoticeCat", "CityNoticeBreakfast", "CityNoticeLostAndFound"
    };

    public static string DistrictModel(int theme)
    {
        return ModelNames[PositiveModulo(theme, DistrictCount)];
    }

    public static string NoticeModel(int variant)
    {
        // Five is coprime with twelve, so a full sequence reaches every notice.
        // Use long arithmetic to keep stable behaviour at either int boundary.
        return ModelNames[DistrictCount + PositiveModulo((long)variant * 5 + 3, NoticeCount)];
    }

    // A few short neighbourhood messages deserve a more readable street presence.
    // Shop signs and district numbers retain their existing visual hierarchy.
    public static float NoticeScale(string model)
    {
        switch (model)
        {
            case "CityNoticeWind":
            case "CityNoticeNeighbours":
            case "CityNoticeYesterday":
            case "CityNoticeCat":
            case "CityNoticeLostAndFound":
                return 1.4f;
            default:
                return 1f;
        }
    }

    static int PositiveModulo(long value, int count)
    {
        return (int)((value % count + count) % count);
    }
}
