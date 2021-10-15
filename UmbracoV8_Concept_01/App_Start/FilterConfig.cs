using System.Web;
using System.Web.Mvc;

namespace UmbracoV8_Concept_01
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
