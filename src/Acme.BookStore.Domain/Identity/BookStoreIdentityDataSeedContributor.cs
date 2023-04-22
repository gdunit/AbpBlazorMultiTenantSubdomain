using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.TenantManagement;

namespace Acme.BookStore.Identity;

    [Dependency(ReplaceServices = true)]
    [ExposeServices(typeof(IdentityDataSeedContributor))]
    public class BookStoreIdentityDataSeedContributor : IdentityDataSeedContributor, ITransientDependency
    {
        private readonly ICurrentTenant CurrentTenant;
        private readonly IRepository<OpenIddictApplication> OpenIdDictApplicationRepository;
        private readonly IRepository<Tenant> TenantRepository;

        public BookStoreIdentityDataSeedContributor(
            IIdentityDataSeeder identityDataSeeder,
            ICurrentTenant currentTenant,
            IRepository<OpenIddictApplication> openIdDictApplicationRepository,
            IRepository<Tenant> tenantRepository
        )
        : base(identityDataSeeder)
        {
            CurrentTenant = currentTenant;
            OpenIdDictApplicationRepository = openIdDictApplicationRepository;
            TenantRepository = tenantRepository;
        }

        public override async Task SeedAsync(DataSeedContext context)
        {
            await base.SeedAsync(context);

            var tenantId = context?.TenantId;

            var tenantName = await TenantRepository
                .GetQueryableAsync()
                .Result
                .Where(x => x.Id == tenantId)
                .Select(x => x.Name).FirstOrDefaultAsync();

            using (CurrentTenant.Change(tenantId))
            {
                if (!tenantName.IsNullOrWhiteSpace())
                {
                    tenantName = tenantName.ToLower();
                    // Add openIdDict redirect and logout URLS
                    var appChanged = false;
                    var blazorOpenIdDictApplication = await OpenIdDictApplicationRepository.FirstOrDefaultAsync(x =>
                        x.ClientId == "BookStore_Blazor");

                    var currentRedirectUris = blazorOpenIdDictApplication.RedirectUris
                        .Replace("[", string.Empty)
                        .Replace("]", string.Empty)
                        .Split(',')
                        .ToList();

                    var tenantRedirectUri =
                        currentRedirectUris.First().Replace("https://", $"https://{tenantName}.");

                    if (!currentRedirectUris.Contains(tenantRedirectUri))
                    {
                        currentRedirectUris.AddLast(tenantRedirectUri);
                        blazorOpenIdDictApplication.RedirectUris = $"[{currentRedirectUris.JoinAsString(",")}]";
                        Console.WriteLine(blazorOpenIdDictApplication.RedirectUris);
                        appChanged = true;
                    }

                    var currentPostLogoutRedirectUris = blazorOpenIdDictApplication.PostLogoutRedirectUris
                        .Replace("[", string.Empty)
                        .Replace("]", string.Empty)
                        .Split(',')
                        .ToList();

                    var tenantLogoutUri =
                        currentPostLogoutRedirectUris.First().Replace("https://", $"https://{tenantName}.");

                    if (!currentPostLogoutRedirectUris.Contains(tenantLogoutUri))
                    {
                        currentPostLogoutRedirectUris.AddLast(tenantLogoutUri);
                        blazorOpenIdDictApplication.PostLogoutRedirectUris =
                            $"[{currentPostLogoutRedirectUris.JoinAsString(",")}]";
                        Console.WriteLine(blazorOpenIdDictApplication.PostLogoutRedirectUris);
                        appChanged = true;
                    }

                    if (appChanged)
                    {
                        await OpenIdDictApplicationRepository.UpdateAsync(blazorOpenIdDictApplication);
                    }
                }
            }
        }
    }