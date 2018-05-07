using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Description;
using TodoList_Service.Models;

namespace TodoList_Service.Controllers
{
    [Authorize]
    public class TodoListController : ApiController
    {
        private static TodoList_ServiceContext db = new TodoList_ServiceContext();

        // GET: api/TodoList
        public IQueryable<Todo> GetTodoes()
        {
            //ClaimsPrincipal.Current.Claims.ToList()
            string userId = ClaimsPrincipal.Current.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value;

            //NO FLOW ON BEHALF

	    //if (ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope").Value != "user_impersonation")
            //{
            //    throw new HttpResponseException(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized, ReasonPhrase = "The Scope claim does not contain 'user_impersonation' or scope claim not found" });
            //}

            ////ClientCredential clientCred = new ClientCredential(clientId, appKey);
            //var bootstrapContext = ClaimsPrincipal.Current.Identities.First().BootstrapContext as BootstrapContext;
            //string userName =
            //    ClaimsPrincipal.Current.FindFirst(System.IdentityModel.Claims.ClaimTypes.Upn) != null ?
            //    ClaimsPrincipal.Current.FindFirst(System.IdentityModel.Claims.ClaimTypes.Upn).Value :
            //    ClaimsPrincipal.Current.FindFirst(System.IdentityModel.Claims.ClaimTypes.Email).Value;

            //string userAccessToken = bootstrapContext.Token;
            ////UserAssertion userAssertion = new UserAssertion(bootstrapContext.Token, "urn:ietf:params:oauth:grant-type:jwt-bearer", userName);

            ////ClientCredential credential = new ClientCredential(clientId, appKey);
            //string tenantID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;
            //string signedInUserID = ClaimsPrincipal.Current.FindFirst(System.IdentityModel.Claims.ClaimTypes.NameIdentifier).Value;


            return db.Todoes.AsQueryable().Where(t => t.Owner.Equals(userId));
        }

        // POST: api/TodoList
        [ResponseType(typeof(Todo))]
        public IHttpActionResult PostTodo(Todo todo)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            todo.Owner = ClaimsPrincipal.Current.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value; 
            db.Todoes.Add(todo);
            //db.SaveChanges();

            return CreatedAtRoute("DefaultApi", new { id = todo.ID }, todo);
        }

        // DELETE: api/TodoList/5
        [ResponseType(typeof(Todo))]
        public IHttpActionResult DeleteTodo(int id)
        {
            Todo todo = db.Todoes.AsQueryable().FirstOrDefault(p=>p.ID == id);
            if (todo == null)
            {
                return NotFound();
            }

            string userId = ClaimsPrincipal.Current.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value;
            if (todo.Owner != userId)
            {
                return Unauthorized();
            }

            db.Todoes.Remove(todo);

            return Ok(todo);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        private bool TodoExists(int id)
        {
            return db.Todoes.Count(e => e.ID == id) > 0;
        }
    }
}