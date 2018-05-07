using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace TodoList_Service.Models
{
    public class TodoList_ServiceContext
    {
        public TodoList_ServiceContext()
        {
            Todoes = new List<Todo>();
        }
        public List<TodoList_Service.Models.Todo> Todoes { get; set; }
    
    }
}
