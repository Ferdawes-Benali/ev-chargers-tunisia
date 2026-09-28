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

/// <summary>Walking band codes (translated by the frontend).</summary>
public static class WalkBands
{
    public const string Min2 = "min2";
    public const string Min5 = "min5";
    public const string Min10 = "min10";
    public const string Min15 = "min15";
    public const string Far = "far";
}

/// <summary>Smart pick codes (translated by the frontend).</summary>
public static class PickKinds
{
    public const string Coffee = "coffee";
    public const string Lunch = "lunch";
    public const string Dinner = "dinner";
    public const string Pray = "pray";
    public const string Walk = "walk";
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
