using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using TodoListService.Models;

namespace TodoListService.Controllers
{
   [Authorize]
    [Route("api/[controller]")]
    public class TodoListController : Controller
    {
        static ConcurrentBag<TodoItem> todoStore = new ConcurrentBag<TodoItem>();

        // GET: api/values
        [HttpGet]
        public IEnumerable<TodoItem> Get()
        {
            //User.Claims.ToList()
            string owner = (User.FindFirst(ClaimTypes.NameIdentifier))?.Value;
            Trace.TraceInformation($"{owner}");
            return todoStore.Where(t => t.Owner == owner).ToList();
        }

        [HttpGet]
        [Route("CallGrapAPI")]
        public async Task<string> CallGrapAPI()
        {
            try
            {
                //User.Claims.ToList()
                ClientCredential clientCred = new ClientCredential(AzureAdOptions.Settings.ClientId, AzureAdOptions.Settings.ClientSecret);

                string token = await HttpContext.GetTokenAsync("access_token");

                string userName = User.FindFirstValue(ClaimTypes.Upn) ?? User.FindFirstValue(ClaimTypes.Email);

                string assertionType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
                var userAssertion = new UserAssertion(token, assertionType, userName);

                AuthenticationContext authContext = new AuthenticationContext(AzureAdOptions.Settings.Authority, new NaiveSessionCache(userName, HttpContext.Session));

                var result = await authContext.AcquireTokenAsync("https://graph.microsoft.com", clientCred, userAssertion);
                var accessToken = result.AccessToken;

                /*   AADSTS65001: The user or administrator has not consented to use
                 *   https://myapps.microsoft.com/ - revoke
                 *   "knownClientApplications": [
                          "a45547e6-8722-4626-a562-766789e6b7c3"
                      ],
                */

                var graphserviceClient = new GraphServiceClient(
                    new DelegateAuthenticationProvider(
                        (requestMessage) =>
                        {
                            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);

                            return Task.FromResult(0);
                        }));

                var items = await graphserviceClient.Me.Request().GetAsync();
                return items.DisplayName;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return "";
        }


        // POST api/values
        [HttpPost]
        public void Post([FromBody]TodoItem Todo)
        {
            string owner = (User.FindFirst(ClaimTypes.NameIdentifier))?.Value;
            todoStore.Add(new TodoItem { Owner = owner, Title = Todo.Title });
        }
    }
}
