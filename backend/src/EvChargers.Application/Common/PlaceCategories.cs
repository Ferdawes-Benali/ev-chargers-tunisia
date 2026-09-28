namespace EvChargers.Application.Common;

public static class PlaceCategories
{
    public const string Cafe = "cafe";
    public const string Restaurant = "restaurant";
    public const string Mosque = "mosque";
    public const string Park = "park";
    public const string Shopping = "shopping";
    public const string Pharmacy = "pharmacy";
    public const string Toilets = "toilets";
    public const string Atm = "atm";
}

/// <summary>What the driver wants to do; each category belongs to one group.</summary>
public static class PlaceGroups
{
    public const string Eat = "eat";
    public const string Pray = "pray";
    public const string Essentials = "essentials";
    public const string Relax = "relax";
    public const string Shop = "shop";

    public static string Of(string category) => category switch
    {
        PlaceCategories.Cafe or PlaceCategories.Restaurant => Eat,
        PlaceCategories.Mosque => Pray,
        PlaceCategories.Park => Relax,
        PlaceCategories.Shopping => Shop,
        _ => Essentials, // toilets, pharmacy, atm
    };
}
