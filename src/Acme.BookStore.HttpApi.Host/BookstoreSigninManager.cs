using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.DependencyInjection;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace Acme.BookStore;

[Dependency(ReplaceServices=true)]
[ExposeServices(typeof(SignInManager<Volo.Abp.Identity.IdentityUser>))]
public class BookstoreSigninManager : Microsoft.AspNetCore.Identity.SignInManager<Volo.Abp.Identity.IdentityUser>
{
    public BookstoreSigninManager(
        Microsoft.AspNetCore.Identity.UserManager<Volo.Abp.Identity.IdentityUser> userManager,
        Microsoft.AspNetCore.Http.IHttpContextAccessor contextAccessor,
        Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<Volo.Abp.Identity.IdentityUser> claimsFactory,
        Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Identity.IdentityOptions> optionsAccessor,
        Microsoft.Extensions.Logging.ILogger<Microsoft.AspNetCore.Identity.SignInManager<Volo.Abp.Identity.IdentityUser>> logger,
        Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider schemes,
        Microsoft.AspNetCore.Identity.IUserConfirmation<Volo.Abp.Identity.IdentityUser> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
    }

    public override async Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent, bool lockoutOnFailure)
    {
        return await base.PasswordSignInAsync(userName, password, isPersistent, lockoutOnFailure);
    }

    public override async Task<SignInResult> PasswordSignInAsync(IdentityUser user, string password, bool isPersistent, bool lockoutOnFailure)
    {
        return await base.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
    }
}