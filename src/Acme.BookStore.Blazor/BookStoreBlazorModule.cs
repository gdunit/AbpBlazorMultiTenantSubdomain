using System;
using System.Linq;
using System.Net.Http;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using IdentityModel;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Volo.Abp.AspNetCore.Components.Web.BasicTheme.Themes.Basic;
using Volo.Abp.AspNetCore.Components.Web.Theming.Routing;
using Volo.Abp.AspNetCore.Components.WebAssembly.BasicTheme;
using Volo.Abp.Autofac.WebAssembly;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;
using Volo.Abp.AutoMapper;
using Volo.Abp.Http.Client;
using Volo.Abp.Identity.Blazor.WebAssembly;
using Volo.Abp.TenantManagement.Blazor.WebAssembly;

namespace Acme.BookStore.Blazor
{
    [DependsOn(
        typeof(AbpAutofacWebAssemblyModule),
        typeof(BookStoreHttpApiClientModule),
        typeof(AbpAspNetCoreComponentsWebAssemblyBasicThemeModule),
        typeof(AbpIdentityBlazorWebAssemblyModule),
        typeof(AbpTenantManagementBlazorWebAssemblyModule)
    )]
    public class BookStoreBlazorModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var environment = context.Services.GetSingletonInstance<IWebAssemblyHostEnvironment>();
            var builder = context.Services.GetSingletonInstance<WebAssemblyHostBuilder>();

            ConfigureAuthentication(builder);
            ConfigureRemoteServices(builder);
            ConfigureHttpClient(context, environment);
            ConfigureBlazorise(context);
            ConfigureRouter(context);
            ConfigureUI(builder);
            ConfigureMenu(context);
            ConfigureAutoMapper(context);
        }

        private void ConfigureRouter(ServiceConfigurationContext context)
        {
            Configure<AbpRouterOptions>(options =>
            {
                options.AppAssembly = typeof(BookStoreBlazorModule).Assembly;
            });
        }

        private void ConfigureMenu(ServiceConfigurationContext context)
        {
            Configure<AbpNavigationOptions>(options =>
            {
                options.MenuContributors.Add(new BookStoreMenuContributor(context.Services.GetConfiguration()));
            });
        }

        private void ConfigureBlazorise(ServiceConfigurationContext context)
        {
            context.Services
                .AddBlazorise()
                .AddBootstrap5Providers()
                .AddFontAwesomeIcons();
        }

        private static void ConfigureAuthentication(WebAssemblyHostBuilder builder)
        {
            builder.Services.AddOidcAuthentication(options =>
            {
                builder.Configuration.Bind("AuthServer", options.ProviderOptions);
                options.UserOptions.NameClaim = OpenIddictConstants.Claims.Name;
                options.UserOptions.RoleClaim = OpenIddictConstants.Claims.Role;
                options.ProviderOptions.DefaultScopes.Add("roles");  
                
                options.ProviderOptions.DefaultScopes.Add("BookStore");
                options.ProviderOptions.DefaultScopes.Add("email");
                options.ProviderOptions.DefaultScopes.Add("phone");
                // Override the domain from the config with the actual tenant specific domain
                options.ProviderOptions.Authority = GetAuthServerAuthorityWithTenantSubDomain(builder);
            });
        }
        
        /*Start tenant subdomain mgt code*/
        private static readonly string[] ProtocolPrefixes = { "http://", "https://" };
        
        private void ConfigureRemoteServices(WebAssemblyHostBuilder builder)
        {
            Configure<AbpRemoteServiceOptions>(options =>
            {
                options.RemoteServices.Default =
                    new RemoteServiceConfiguration(GetApiServerAuthorityWithTenantSubDomain(builder));
            });
        }
        
        private static string GetAuthServerAuthorityWithTenantSubDomain(WebAssemblyHostBuilder builder)
        {
            return ConvertToTenantSubDomain(builder, "AuthServer:Authority");
        }
        
        private static string GetApiServerAuthorityWithTenantSubDomain(WebAssemblyHostBuilder builder)
        {
            return ConvertToTenantSubDomain(builder, "RemoteServices:Default:BaseUrl");
        }

        private static string ConvertToTenantSubDomain(WebAssemblyHostBuilder builder, string configPath)
        {
            var baseUrl = builder.HostEnvironment.BaseAddress;
            var configUrl = builder.Configuration[configPath];
            return configUrl.Replace("{0}.", GetTenantName(baseUrl));
        }
        
        private static string GetTenantName(string baseUrl)
        {
            var hostName = baseUrl.RemovePreFix(ProtocolPrefixes);
            var urlSplit = hostName.Split('.');
            return urlSplit.Length % 2 == 0 ? null : $"{urlSplit.FirstOrDefault()}.";
        }        
        /*END tenant subdomain mgt code*/

        private static void ConfigureUI(WebAssemblyHostBuilder builder)
        {
            builder.RootComponents.Add<App>("#ApplicationContainer");
        }

        private static void ConfigureHttpClient(ServiceConfigurationContext context, IWebAssemblyHostEnvironment environment)
        {
            context.Services.AddTransient(sp => new HttpClient
            {
                BaseAddress = new Uri(environment.BaseAddress)
            });
        }

        private void ConfigureAutoMapper(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options =>
            {
                options.AddMaps<BookStoreBlazorModule>();
            });
        }
    }
}
