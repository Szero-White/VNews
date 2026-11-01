using System.Security.Claims;
using VNews.Domain.Identity;

namespace VNews.Web.Helpers;

public static class UserExtensions
{
    /// <summary>True for anyone who works in the newsroom (Author, Editor or Admin). Used to decide which links to offer; pages still authorize on the server.</summary>
    public static bool IsNewsroomStaff(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Editor) || user.IsInRole(Roles.Author);
}
