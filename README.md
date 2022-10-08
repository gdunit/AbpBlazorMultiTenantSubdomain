# Abp Blazor Multi-Tenant Subdomain Resolution

A common requirement for software-as-a-service applications is to be able to provide a specific URL to each tenant that will take them directly to their portion of the application, often together with customised branding or data for their company.

Here we will describe a method of implementing per-tenant subdomains for Blazor UI using the ABP framework.

For this case we will consider that we have a non-tiered solution (no separate auth server).
If you have a tiered solution, you will need to repeat steps 2 and 3 as appropriate for that module.

We will also consider that we are going to use mybookstore.com as a base domain.

See the completed solution for this example at [this github repository](https://github.com/gdunit/AbpBlazorMultiTenantSubdomain "Example Repository").

### Step 1 
DbMigrator - appsettings.json
- Amend the Clients inside the IdentityServer config settings. 
- For each client, amend the RootUrl, noting the {0} where the tenant domain will be inserted:

```
  "RootUrl": "https://{0}.mybookstore.com:{port number goes here}"
```

Important: Run the DBMigrator to ensure that the clients are updated within the auth server.


### Step 2
Now we will amend the AppSettings.json file for the HttpApi.Host project. 
Note the addition of the SelfUrlWithoutTenant property:

```
  "App": {
    "SelfUrl": "https://{0}.api.mybookstore.com:44399",
    "SelfUrlWithoutTenant": "https://api.mybookstore.com:44399",
    "CorsOrigins": "https://*.mybookstore.com,http://*.mybookstore.com:4200,https://*.mybookstore.com:44307,http://mybookstore.com:4200,https://mybookstore.com:44307"
  },
```


### Step 3
Now, we will amend the HttpApiHost module class' ConfigureServices() method:

```
  context.Services.AddAbpStrictRedirectUriValidator();
  context.Services.AddAbpClientConfigurationValidator();
  context.Services.AddAbpWildcardSubdomainCorsPolicyService();
  Configure<AbpTenantResolveOptions>(options =>
  {
      options.AddDomainTenantResolver(configuration["App:SelfUrl"]);
  });

  Configure<IdentityServerOptions>(options =>
  {
      options.IssuerUri = configuration["App:SelfUrlWithoutTenant"];
  });
```
And add the following two lines to the AddAuthentication() configuration as follows:

```
  context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
      // Add new config options as below
      // Other options already there should be left as-is 
      // See https://github.com/abpframework/abp/issues/3304
      options.TokenValidationParameters.ValidateIssuer = false;
      options.TokenValidationParameters.ValidateAudience = false;
  });
```

### Step 4
Now, we are done with the back end! We will change the Blazor project's appsettings.json:

```
  "App": {
    "SelfUrl": "https://{0}.mybookstore.com:44307"
  },
  "AuthServer": {
    "Authority": "https://{0}.api.mybookstore.com:44399",
    "ClientId": "BookStore_Blazor",
    "ResponseType": "code"
  },
  "RemoteServices": {
    "Default": {
      "BaseUrl": "https://{0}.api.mybookstore.com:44399"
    }
  },
```

### Step 5
Amend the Blazor project's module class, ConfigureServices() again:
Here, we cannot use a tenant resolver directly as it is not supported. 
But because the app will initialise on the client, we can set the URL at startup.

Add the following fields & methods:

```
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

  // This is a naive implementation which assumes [tenantdomain].[domain].[suffix]
  private static string GetTenantName(string baseUrl)
  {
      var hostName = baseUrl.RemovePreFix(ProtocolPrefixes);
      var urlSplit = hostName.Split('.');
      // If the url has two (or even) splits ([domain].[suffix]) we assume the host.
      // If three (or odd), then we assume a tenant subomain is added and add the period also.
      return urlSplit.Length % 2 == 0 ? null : $"{urlSplit.FirstOrDefault()}.";
  }        
```

Amend the ConfigureAuthentication() options to add the new line at the bottom shown here:
```
  private static void ConfigureAuthentication(WebAssemblyHostBuilder builder)
  {
      builder.Services.AddOidcAuthentication(options =>
      {
          builder.Configuration.Bind("AuthServer", options.ProviderOptions);
          options.UserOptions.RoleClaim = JwtClaimTypes.Role;
          options.ProviderOptions.DefaultScopes.Add("BookStore");
          options.ProviderOptions.DefaultScopes.Add("role");
          options.ProviderOptions.DefaultScopes.Add("email");
          options.ProviderOptions.DefaultScopes.Add("phone");
          // Add this line here
          // Override the domain from the config with the actual tenant specific domain
          options.ProviderOptions.Authority = GetAuthServerAuthorityWithTenantSubDomain(builder);
      });
  }
```

Finally, add the call to ConfigureRemoteServices() underneath ConfigureAuthentication():

```
  ConfigureRemoteServices(builder);
```

### Step 6
Now, in order to make this run locally we need to:

Amend the hosts file on your computer to add the custom domains, here we imagine there is the host and one tenant, tenant1 (change as needed for your case):
```
  127.0.0.1 mybookstore.com
  127.0.0.1 tenant1.mybookstore.com
  127.0.0.1 api.mybookstore.com
  127.0.0.1 tenant1.api.mybookstore.com
```

Finally, amend the applicationhost.config file. 
In the <sites> node, amend the <binding> for each to make it a wildcard. This will let you access subdomains from the parent locally via IIS Express. 
Note that there is no domain stated here, just the port:
```
  <binding protocol="https" bindingInformation="*:44307:" />
```
```
  <binding protocol="https" bindingInformation="*:44399:" />
```

Important Note: If you are running JetBrains Rider as an IDE, you may need to uncheck the "Generate ApplicationHost.config" checkbox in the "Edit Configuration" options for each runnable project. Otherwise, rider will overwrite the file on each run.

### Step 7
Finally you can run the application:
- Run the host as admin
- Add a tenant (tenant1)
- Logout
- Go to tenant1.mybookstore.com
- You should be redirected to tenant1.api.mybookstore.com. Login as the tenant admin user
- You should now be logged into the tenant and redirected to the Blazor homepage!
