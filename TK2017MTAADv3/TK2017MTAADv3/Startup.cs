using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TK2017MTAADv3.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.ApplicationInsights;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.Graph;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace TK2017MTAADv3
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            /*
             * https://portal.azure.com/#blade/Microsoft_AAD_IAM/ApplicationBlade/objectId/8aa9350a-d919-4fb2-8eb2-5af69c2469e0/appId/3a2eaceb-1ff3-49a4-9156-dc7fd7b15409
             * 
             */
            string authority = Configuration["AzureAd:AzureAdInstance"];// + Configuration["AzureAd:Domain"];
            services.AddApplicationInsightsTelemetry(Configuration);
            var str = Configuration.GetConnectionString("DefaultConnection");
            Trace.TraceError(str);
            Trace.Flush();

            services.Configure<AzureAdOptions>(Configuration.GetSection("AzureAD"));

            //str = "Server=tcp:nynftttf24v12.database.windows.net,1433;Initial Catalog=tkDemoLoadToSQLDB;Persist Security Info=False;User ID=tkadmin;Password=aA12weRT5$$L;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
            //DB
            services.AddDbContextPool<TenantContext>(
                options => options.UseSqlServer(str));


            var sp = services.BuildServiceProvider();
            var db = sp.GetService<TenantContext>();
            db.Database.EnsureCreated();


            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            //.AddAzureAd(options => Configuration.Bind("AzureAd", options))
            .AddCookie()
            .AddOpenIdConnect(o =>
            {
                o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                o.ClientId = Configuration["AzureAD:ClientId"];
                o.Authority = authority;

                //Neccessary to get code!!!
                o.ClientSecret = Configuration["AzureAD:ClientSecret"];
                //o.SignedOutRedirectUri = Configuration["AzureAd:PostLogoutRedirectUri"];
                o.ResponseType = "code id_token";

                //token = "oauth2AllowImplicitFlow": true, and for JavaScript
                //"code id_token" 

                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false
                };
                o.Events = new OpenIdConnectEvents
                {
                    OnRemoteSignOut = (context) =>
                    {
                        return Task.FromResult(0);
                    },
                    OnRedirectToIdentityProvider = (context) =>
                    {
                        //string appBaseUrl = context.Request.Scheme + "://" + context.Request.Host + context.Request.PathBase + "/";
                        //context.ProtocolMessage.RedirectUri = appBaseUrl;
                        //context.ProtocolMessage.PostLogoutRedirectUri = appBaseUrl;
                        return Task.FromResult(0);
                    },
                    OnTokenValidated = async (context) =>
                    {
                        var tid = context.SecurityToken.Claims.FirstOrDefault(p => p.Type == "tid").Value.ToLower(); //Tenant
                        if (tid == "a757c7b8-69a2-4b92-b277-be767fc38487") return; //Own
                        var tenantdb = await db.Tenants.FirstOrDefaultAsync(p => p.TenantGuid == tid);
                        if (tenantdb == null)
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            //context.Response.Redirect("/Home/Error");
                            context.HandleResponse();
                        }

                        return;
                    },
                    OnTokenResponseReceived = (context) =>
                    {
                        Debug.WriteLine(context);
                        return Task.FromResult(0);
                    },
                    OnAuthorizationCodeReceived = async (context) =>
                    {
                        Debug.WriteLine(context.TokenEndpointRequest.Code);
                        try
                        {

                            var authContext = new AuthenticationContext($"{Configuration["AzureAD:AzureAdInstance"]}");
                            var creds = new ClientCredential(Configuration["AzureAD:ClientId"], Configuration["AzureAD:ClientSecret"]);
                            string p = $"{Configuration["AzureAD:Domain"] + "/signin-oidc"}";
                            var redirectUri = new Uri(p); //The same as during login!!!

                            //Because we have no Implicit Flow enabled!
                            var authResult = await authContext.AcquireTokenByAuthorizationCodeAsync(
                                context.TokenEndpointRequest.Code, redirectUri, creds,
                                "https://graph.microsoft.com/");

                            var gsc = new GraphServiceClient(
                                new DelegateAuthenticationProvider(
                                    (requestMessage) =>
                                    {
                                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", authResult.AccessToken);

                                        return Task.FromResult(0);
                                    }));


                            //This can be done by any user (no admin consent)
                            var me = await gsc.Me.Request().GetAsync();

                            //Including Transitive, DirectoryObjects!
                            //var objects = await gsc.DirectoryObjects.GetByIds(me.ToList(), new string[] { "group", "directoryRole" }).Request().PostAsync();

                            //To read groups - we need admin consent
                            //Get group name require admin consent
                            var myGroup = await gsc.Me.MemberOf.Request().GetAsync();
                            var claimsIdentity = (ClaimsIdentity)context.Principal.Identity;
                            foreach (var item in myGroup)
                            {
                                switch (item)
                                {
                                    case Microsoft.Graph.Group group:
                                        claimsIdentity.AddClaim(new Claim("tkgroups", group.DisplayName));
                                        break;
                                    case Microsoft.Graph.DirectoryRole role:
                                        claimsIdentity.AddClaim(new Claim("tkgroups", role.DisplayName));
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }

                        //return Task.FromResult(0);
                    },
                    OnAuthenticationFailed = (context) =>
                    {
                        context.Response.Redirect("/Home/Error");
                        context.HandleResponse();
                        return Task.FromResult(0);
                    }

                };
            });

            services.AddMvc();


            //Registration: https://portal.azure.com/#blade/Microsoft_AAD_IAM/ApplicationBlade/objectId/8aa9350a-d919-4fb2-8eb2-5af69c2469e0/appId/3a2eaceb-1ff3-49a4-9156-dc7fd7b15409
            //AppID: https://tkdxpl.onmicrosoft.com/TK2017MTAADv3
            //
            List<string> groupGuid = new List<string>();
            groupGuid.Add("8542e184-3375-49de-8401-131a73ed9d9c");
            ///tkopaczmse3             da2d4106-4bd5-4068-b2f1-8e47c7b8fe71, TKTEST1 - tkopaczms@tkopaczmse3.onmicrosoft.com
            ///tkdpepl.onmicrosoft.com a668c53a-0586-4abd-8732-13d6dc03c7ae, GROUP1 - tk@tkdpepl.onmicrosoft.com
            ///tkdxpl1.onmicrosoft.com f25fcc71-d538-4140-babf-32bfa7d599a1, tkdxpl1group tkopacztkdxpl1@tkdxpl1.onmicrosoft.com 
            //Ugly, demo only - should be dynamics! After adding new tenant we need to restart app!
            try
            {
                foreach (var item in db.Tenants.Where(p => p.TenantGuid != ""))
                {
                    groupGuid.Add(item.GroupGuid);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                throw;
            }
            services.AddAuthorization(options =>
            {
                //In general - for single tenant, where we can control "names" of groups
                //OneCla
                options.AddPolicy("AdminPolicy", policy => policy.RequireClaim("tkgroups", new string[] { "Admin", "tkdxpl1group" }));
                options.AddPolicy("Admin1Policy", policy => policy.RequireClaim("tkgroups", "Admin1"));
                //Require groupMembershipClaims in manifest
                //Guid from: https://portal.azure.com/?r=1#blade/Microsoft_AAD_IAM/GroupDetailsMenuBlade/Properties/groupId/8542e184-3375-49de-8401-131a73ed9d9c
                options.AddPolicy("AdminPolicyByGuid", policy => policy.RequireClaim("groups", groupGuid));
            });
            //https://portal.office.com/account/#apps, App Permission, for user
            //https://portal.office.com/myapps <-admin
            //https://portal.azure.com/#blade/Microsoft_AAD_IAM/EnterpriseApplicationListBlade <- admin, enterprise apps (after sign up)
            //As Admin:
            //https://manage.windowsazure.com/@tkopaczmsE3.onmicrosoft.com#Workspaces/ActiveDirectoryExtension/Directory/a07319e7-7cb1-41fe-9ebf-250e5deba957/apps


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseRewriter(new RewriteOptions().AddRedirectToHttpsPermanent());
            //V2 - internal
            //app.UseApplicationInsightsRequestTelemetry();
            //app.UseApplicationInsightsExceptionTelemetry();
            app.UseDeveloperExceptionPage();
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=MT}/{action=Index}/{id?}");
            });
        }
    }
}
