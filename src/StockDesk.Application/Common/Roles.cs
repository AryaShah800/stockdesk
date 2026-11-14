namespace StockDesk.Application.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = [Admin, Staff];
}
