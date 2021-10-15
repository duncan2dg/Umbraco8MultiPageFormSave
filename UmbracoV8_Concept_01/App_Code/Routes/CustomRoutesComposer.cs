using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Umbraco.Core.Composing;
using Umbraco.Forms.Core;
using Umbraco.Forms.Web.Controllers;

namespace UmbracoV8_Concept_01.App_Code
{
    public class CustomRoutesComposer : ComponentComposer<CustomRoutesComponent>
    {
    }

    public class CustomRoutesComponent : IComponent
    {
        public void Initialize()
        {
            UmbracoFormsController.FormPrePopulate += (object sender, FormEventArgs e) =>
            {
                // nothing needed here, it just needs to exist to save all form data when submitting a form with multiple form pages
            };
        }

        public void Terminate()
        {
            throw new NotImplementedException();
        }
    }
}