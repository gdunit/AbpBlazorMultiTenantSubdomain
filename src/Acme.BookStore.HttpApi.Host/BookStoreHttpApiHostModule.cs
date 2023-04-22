using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Acme.BookStore.EntityFrameworkCore;
using Acme.BookStore.MultiTenancy;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Microsoft.OpenApi.Models;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using Owl.TokenWildcardIssuerValidator;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.WildcardDomains;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace Acme.BookStore
{
    [DependsOn(
        typeof(BookStoreHttpApiModule),
        typeof(AbpAutofacModule),
        typeof(AbpAspNetCoreMultiTenancyModule),
        typeof(BookStoreApplicationModule),
        typeof(BookStoreEntityFrameworkCoreModule),
        typeof(AbpAspNetCoreMvcUiBasicThemeModule),
        typeof(AbpAccountWebOpenIddictModule),
        typeof(AbpAspNetCoreSerilogModule),
        typeof(AbpSwashbuckleModule)
    )]
    public class BookStoreHttpApiHostModule : AbpModule
    {
        private const string DefaultCorsPolicyName = "Default";

        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            var hostingEnvironment = context.Services.GetHostingEnvironment();
            
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                if (hostingEnvironment.IsProduction() || hostingEnvironment.IsStaging())
                {
                    //https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html
                    options.AddDevelopmentEncryptionAndSigningCertificate = false;
                }
                else
                {
                    options.AddDevelopmentEncryptionAndSigningCertificate = true;
                }
            });
            
            // Needed for production only
            // PreConfigure<OpenIddictServerBuilder>(options =>
            // {
            //     if (hostingEnvironment.IsProduction() || hostingEnvironment.IsStaging())
            //     {
            //         options.AddEncryptionCertificate(LoadCertificate(
            //             configuration["AuthServer:EncryptionCertificateThumbprint"]));
            //         options.AddSigningCertificate(LoadCertificate(
            //             configuration["AuthServer:SigningCertificateThumbprint"]));
            //     }
            // });
            
            PreConfigure<OpenIddictBuilder>(builder =>
            {
                builder.AddValidation(options =>
                {
                    options.AddAudiences("BookStore");
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });
            });
            
            PreConfigure<IdentityBuilder>(identityBuilder =>
            {
                identityBuilder.AddSignInManager<BookstoreSigninManager>();
            });

            // var frontEndUrlWithSubdomainPlaceholder = configuration["App:FrontEndUrl"];
            //
            // PreConfigure<AbpOpenIddictWildcardDomainOptions>(options =>
            // {
            //     options.EnableWildcardDomainSupport = true;
            //     options.WildcardDomainsFormat.Add($"{frontEndUrlWithSubdomainPlaceholder}/signin-oidc");
            //     options.WildcardDomainsFormat.Add($"{frontEndUrlWithSubdomainPlaceholder}/signout-callback-oidc");
            // });
        }

        // For linux azure app service, path differs for windows
        // private X509Certificate2 LoadCertificate(string thumbprint)
        // {
        //     var bytes = File.ReadAllBytes($"/var/ssl/private/{thumbprint}.p12");
        //     return new X509Certificate2(bytes);
        // }
        
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            ConfigureAuthentication(context);
            
            /*Start Subdomain Management Code*/
            var selfDomainWithSubdomainPlaceholder = configuration["App:SelfUrlWithSubdomain"];
            var selfDomain = configuration["App:SelfUrl"];
            Configure<AbpTenantResolveOptions>(options =>
            {
                options.AddDomainTenantResolver(selfDomainWithSubdomainPlaceholder);
            });

            var frontEndUrlWithSubdomainPlaceholder = configuration["App:FrontEndUrl"];
            Configure<AbpOpenIddictWildcardDomainOptions>(options =>
            {
                options.EnableWildcardDomainSupport = true;
                options.WildcardDomainsFormat.Add($"{frontEndUrlWithSubdomainPlaceholder}/signin-oidc");
                options.WildcardDomainsFormat.Add($"{frontEndUrlWithSubdomainPlaceholder}/signout-callback-oidc");
            });
            
            Configure<OpenIddictServerOptions>(options =>
            {
                options.TokenValidationParameters.IssuerValidator = TokenWildcardIssuerValidator.IssuerValidator;
                options.TokenValidationParameters.ValidIssuers = new[]
                {
                    selfDomain.EnsureEndsWith('/'),
                    selfDomainWithSubdomainPlaceholder.EnsureEndsWith('/')
                };
            });
            /*END Subdomain Management Code*/
            
            ConfigureBundles();
            ConfigureUrls(configuration);
            ConfigureConventionalControllers();
            ConfigureLocalization();
            ConfigureVirtualFileSystem(context);
            ConfigureCors(context, configuration);
            ConfigureSwaggerServices(context);
        }

        private void ConfigureBundles()
        {
            Configure<AbpBundlingOptions>(options =>
            {
                options.StyleBundles.Configure(
                    BasicThemeBundles.Styles.Global,
                    bundle => { bundle.AddFiles("/global-styles.css"); }
                );
            });
        }

        private void ConfigureUrls(IConfiguration configuration)
        {
            Configure<AppUrlOptions>(options =>
            {
                options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            });
        }

        private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();

            if (hostingEnvironment.IsDevelopment())
            {
                Configure<AbpVirtualFileSystemOptions>(options =>
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<BookStoreDomainSharedModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}Acme.BookStore.Domain.Shared"));
                    options.FileSets.ReplaceEmbeddedByPhysical<BookStoreDomainModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}Acme.BookStore.Domain"));
                    options.FileSets.ReplaceEmbeddedByPhysical<BookStoreApplicationContractsModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}Acme.BookStore.Application.Contracts"));
                    options.FileSets.ReplaceEmbeddedByPhysical<BookStoreApplicationModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}Acme.BookStore.Application"));
                });
            }
        }

        private void ConfigureConventionalControllers()
        {
            Configure<AbpAspNetCoreMvcOptions>(options =>
            {
                options.ConventionalControllers.Create(typeof(BookStoreApplicationModule).Assembly);
            });
        }

        private void ConfigureAuthentication(ServiceConfigurationContext context)
        {
            context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        }
        
        // private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
        // {
        //     context.Services.AddAuthentication()
        //         .AddJwtBearer(options =>
        //         {
        //             options.Authority = configuration["AuthServer:Authority"];
        //             options.RequireHttpsMetadata = Convert.ToBoolean(configuration["AuthServer:RequireHttpsMetadata"]);
        //             options.Audience = "BookStore";
        //             options.TokenValidationParameters.ValidateIssuer = false;
        //             options.TokenValidationParameters.ValidateAudience = false;
        //             options.BackchannelHttpHandler = new HttpClientHandler
        //             {
        //                 ServerCertificateCustomValidationCallback =
        //                     HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        //             };
        //         });
        // }

        private static void ConfigureSwaggerServices(ServiceConfigurationContext context)
        {
            context.Services.AddSwaggerGen(
                options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo {Title = "BookStore API", Version = "v1"});
                    options.DocInclusionPredicate((docName, description) => true);
                });
        }

        private void ConfigureLocalization()
        {
            Configure<AbpLocalizationOptions>(options =>
            {
                options.Languages.Add(new LanguageInfo("ar", "ar", "العربية"));
                options.Languages.Add(new LanguageInfo("cs", "cs", "Čeština"));
                options.Languages.Add(new LanguageInfo("en", "en", "English"));
                options.Languages.Add(new LanguageInfo("fr", "fr", "Français"));
                options.Languages.Add(new LanguageInfo("hu", "hu", "Magyar"));
                options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Português"));
                options.Languages.Add(new LanguageInfo("ru", "ru", "Русский"));
                options.Languages.Add(new LanguageInfo("tr", "tr", "Türkçe"));
                options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
                options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
                options.Languages.Add(new LanguageInfo("de-DE", "de-DE", "Deutsche", "de"));
            });
        }

        private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddCors(options =>
            {
                options.AddPolicy(DefaultCorsPolicyName, builder =>
                {
                    builder
                        .WithOrigins(
                            configuration["App:CorsOrigins"]
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray()
                        )
                        .WithAbpExposedHeaders()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAbpRequestLocalization();

            if (!env.IsDevelopment())
            {
                app.UseErrorPage();
            }

            app.UseCorrelationId();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors(DefaultCorsPolicyName);
            app.UseAuthentication();
            app.UseAbpOpenIddictValidation();

            if (MultiTenancyConsts.IsEnabled)
            {
                app.UseMultiTenancy();
            }

            app.UseAuthorization();

            app.UseSwagger();
            app.UseAbpSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "BookStore API");
            });

            app.UseAuditing();
            app.UseAbpSerilogEnrichers();
            app.UseConfiguredEndpoints();
        }
    }
}
