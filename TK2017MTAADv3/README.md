TODO OPIS - UAKTUALNIC
# Description in progres, code is working
# 2017-AAD-MultiTenant-Policy-NETCore
## Summary
How to build a simple, policy-based authorization in the multitenat application. Using Azure Active Directory. Using .NET Core 2.0 Preview 1
## Project Structure
Application is registered in tkdxpl.onmicrosoft.com, as multitenant
URL: https://localhost:44359/ 

### Azure Active Directory manifest

Azure Active Directory manifest (https://portal.azure.com, Azure Active Director, App Registration), need change groupMembershipClaims to value: groups, **all** or 7:
```json
{
...
  "groupMembershipClaims": "All",
  "availableToOtherTenants": true,
...
}
```
![AAD UI for App Registration](/IMG/2017-07-13_19-11-04 AAD Registration UI.png)
![AAD Manifest](/IMG/2017-07-13_19-17-45 AAD Registration manifest.png)

### Startup.cs
Startup.cs - DB Context for EF Core
```csharp
//DB
services.AddDbContextPool<TenantContext>(
    options => options.UseSqlServer(
        Configuration.GetConnectionString("DefaultConnection")));
```
Startup.cs - get group guid from registration db (should be dynamics - demo here)
```csharp
List<string> groupGuid = new List<string>();
groupGuid.Add("8542e184-3375-49de-8401-131a73ed9d9c");
var sp = services.BuildServiceProvider();
var db = sp.GetService<TenantContext>();
db.Database.EnsureCreated();
foreach (var item in db.Tenants.Where(p => p.TenantGuid != ""))
{
    groupGuid.Add(item.GroupGuid);
}
```

Startup.cs - setup authorization and policies
```csharp
...
services.AddAuthorization(options =>
{
    //In general - for single tenant, where we can control "names" of groups
    options.AddPolicy("AdminPolicy", policy => policy.RequireClaim("tkgroups", "Admin"));
    options.AddPolicy("Admin1Policy", policy => policy.RequireClaim("tkgroups", "Admin1"));
                
    //Require groupMembershipClaims in manifest
    options.AddPolicy("AdminPolicyByGuid", policy => policy.RequireClaim("groups", groupGuid));
});
```
https://portal.office.com/account/#apps, App Permission, for user
https://portal.office.com/myapps <-admin
https://portal.azure.com/#blade/Microsoft_AAD_IAM/EnterpriseApplicationListBlade <- admin, enterprise apps (after sign up)
As Admin:
https://manage.windowsazure.com/@tkopaczmsE3.onmicrosoft.com#Workspaces/ActiveDirectoryExtension/Directory/a07319e7-7cb1-41fe-9ebf-250e5deba957/apps

### Use policies in HomeContoller.cs

1. [Authorize] for class - to enable authorization
2. [AllowAnonymous] for unrestricted elements
3. [Authorize(Policy = "GroupPolicyByGuid")] - to force policy for specific method

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TK2017STAADv2.Models;
using System.Security.Claims;

namespace TK2017STAADv2.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            var claims = ((ClaimsIdentity)User.Identity).Claims;
            return View((object)claims);
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        [AllowAnonymous]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /*Policies*/
        [Authorize(Policy = "OKPolicy")]
        public IActionResult DemoOKPolicy()
        {
            ViewData["Message"] = "OK - AdminPolicy";
            return View("Demo");
        }

        [Authorize(Policy = "GroupPolicyByGuid")]
        public IActionResult DemoAdminPolicyByGuid()
        {
            ViewData["Message"] = "Demo - GroupPolicyByGuid";
            return View("Demo");
        }


    }
}
```

### AzureAdOpenIdConnectOptionsSetup.cs

Configure OpenID Connect. Important:
1. We need code and id_token, *oidcOptions.ResponseType = "code id_token";*
2. To get code, we need both clientid and secret: *oidcOptions.ClientId = _aadOptions.ClientId; oidcOptions.ClientSecret = _aadOptions.ClientSecret;*
3. We can validate Principal - see *myUserValidationLogic*
4. We can add additional claims and query Graph API, see OpenID event *OnAuthorizationCodeReceived*. To query Graph API we need bearer token - see AzureAuthenticationProvider.cs

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Graph;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdOpenIdConnectOptionsSetup : IConfigureOptions<OpenIdConnectOptions>
    {
        private readonly AzureAdOptions _aadOptions;

        public AzureAdOpenIdConnectOptionsSetup(IOptions<AzureAdOptions> aadOptions)
        {
            _aadOptions = aadOptions.Value;
        }

        public void Configure(OpenIdConnectOptions oidcOptions)
        {
            oidcOptions.ClientId = _aadOptions.ClientId;
            oidcOptions.Authority = _aadOptions.Authority;
            oidcOptions.UseTokenLifetime = true;
            oidcOptions.CallbackPath = _aadOptions.CallbackPath;
            oidcOptions.ClientSecret = _aadOptions.ClientSecret;

            //We need id_token (login) and code (to call Graph API / another Web Api)
            oidcOptions.ResponseType = "code id_token";

            oidcOptions.Events = new OpenIdConnectEvents
            {
                OnTicketReceived = (context) =>
                {
                    // If your authentication logic is based on users then add your logic here
                    return Task.FromResult(0);
                },
                OnAuthenticationFailed = (context) =>
                {
                    context.Response.Redirect("/Home/Error");
                    context.HandleResponse(); // Suppress the exception
                    return Task.FromResult(0);
                },
                // If your application needs to do authenticate single users, add your user validation below.
                OnTokenValidated = (context) =>
                {
                    return myUserValidationLogic(context.Ticket.Principal);
                },
                OnAuthorizationCodeReceived = (context) =>
                {
                    Task.Run(async () =>
                    {
                        Debug.WriteLine(context.TokenEndpointRequest.Code);
                        try
                        {
                            List<Claim> claims = new List<Claim>();
                            var gsc = new GraphServiceClient(new AzureAuthenticationProvider(_aadOptions, context.Ticket.Principal, context.TokenEndpointRequest.Code));

                            var me = await gsc.Me.Request().GetAsync();
                            if (me.JobTitle == "jobtitle")
                            {
                                //Add any claim based on Graph API - 
                                claims.Add(new Claim("tkclaim", "ok"));
                            }

                            //Update principal
                            var principal = context.Ticket.Principal;
                            //Add Claims
                            (principal.Identity as ClaimsIdentity).AddClaims(claims);
                            //Replace ticket
                            context.Ticket = new AuthenticationTicket(
                                                 principal,
                                                 context.Ticket.Properties,
                                                 context.Ticket.AuthenticationScheme);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }
                    }).Wait();

                    return Task.FromResult(0);
                }
            };
        }

        private Task myUserValidationLogic(ClaimsPrincipal principal)
        {
            //Or check in DB or...
            if (principal.Identity.Name == "ABC") throw new UnauthorizedAccessException();
            return Task.FromResult(0);
        }
    }
}

```

### AzureAuthenticationProvider.cs (new file)

Class will add bearer token to http request for AAD graph api. Need - ClaimsPrincipal (based od OpenID id_token) and code for graph api.

```csharp
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;

namespace Microsoft.AspNetCore.Authentication.Extensions
{
    internal class AzureAuthenticationProvider : IAuthenticationProvider
    {
        private AzureAdOptions m_aadOptions;
        private ClaimsPrincipal m_principal;
        private string m_code;

        public AzureAuthenticationProvider(AzureAdOptions aadOptions, ClaimsPrincipal principal, string code)
        {
            m_aadOptions = aadOptions;
            this.m_principal = principal;
            this.m_code = code;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            string signedInUserID = m_principal.FindFirst(ClaimTypes.NameIdentifier).Value;
            //This will work for multitenant apps
            string tenantID = m_principal.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;
            //This will work only for single tenant app
            //string tenantID = m_aadOptions.TenantId;
            var authContext = new AuthenticationContext($"{m_aadOptions.AzureAdSingleInstance}{tenantID}");
            var creds = new ClientCredential(m_aadOptions.ClientId, m_aadOptions.ClientSecret);
            var redirectUri = new Uri($"{m_aadOptions.CallbackDomain}{m_aadOptions.CallbackPath}");
            var authResult = await authContext.AcquireTokenByAuthorizationCodeAsync(
                m_code, redirectUri, creds,
                "https://graph.microsoft.com/");

            request.Headers.Add("Authorization", "Bearer " + authResult.AccessToken);
        }
    }
}
```
## Remarks
To delete consent, go https://portal.office.com/account/#apps


## Users to test

| Login                               | Password  | description                              |
| ----------------------------------- | --------- | ---------------------------------------- |
| demopolicyok@tkdxpl.onmicrosoft.com | Has@lo1#q | Job Title atrribute is equal to *jobtitle* |
| demoadmin1@tkdxpl.onmicrosoft.com   | Has@lo1#q | User belong to group 57fda17b-7e8d-4ba6-8e0d-8a8fe4539564 |
