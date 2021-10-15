using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Scoping;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Attributes;
using Umbraco.Forms.Core.Data.Storage;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Extensions;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Providers;
using Umbraco.Forms.Core.Services;
using Umbraco.Forms.Mvc;
using Umbraco.Forms.Mvc.BusinessLogic;
using Umbraco.Forms.Mvc.Models;
using Umbraco.Forms.Web.Models;
using Umbraco.Web;
using Umbraco.Web.Mvc;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;


namespace UmbracoV8_Concept_01.App_Code.Controllers
{
    public class FormsController : SurfaceController
    {
        private readonly IFormStorage _formStorage;

        private readonly IRecordStorage _recordStorage;

        private readonly IRecordService _recordService;

        private readonly IFacadeConfiguration _configuration;

        private readonly FieldCollection _fieldCollection;

        private readonly IFieldTypeStorage _fieldTypeStorage;

        private readonly IFieldPreValueSourceService _fieldPreValueSourceService;

        private readonly IFieldPreValueSourceTypeService _fieldPreValueSourceTypeService;

        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        private readonly IPageService _pageService;

        private readonly IScopeProvider _scopeProvider;

        private const string FormsFormKey = "umbracoformsform";

        public FormsController(
            IFormStorage formStorage,
            IRecordStorage recordStorage,
            IRecordService recordService,
            IFacadeConfiguration configuration,
            FieldCollection fieldCollection,
            IFieldTypeStorage fieldTypeStorage,
            IFieldPreValueSourceService fieldPreValueSourceService,
            IFieldPreValueSourceTypeService fieldPreValueSourceTypeService,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPageService pageService,
            IScopeProvider scopeProvider)
        {
            _formStorage = formStorage;
            _recordStorage = recordStorage;
            _recordService = recordService;
            _configuration = configuration;
            _fieldCollection = fieldCollection;
            _fieldTypeStorage = fieldTypeStorage;
            _fieldPreValueSourceService = fieldPreValueSourceService;
            _fieldPreValueSourceTypeService = fieldPreValueSourceTypeService;
            _umbracoContextAccessor = umbracoContextAccessor;
            _pageService = pageService;
            _scopeProvider = scopeProvider;
        }

        private static string Base64Decode(string base64EncodedData)
        {
            byte[] numArray = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(numArray);
        }

        private static string Base64Encode(string plainText)=>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        

        private void ClearFormModel()
        {
            TempData.Remove("umbracoformsform");
        }

        private void ClearFormState(FormViewModel model)
        {
            model.RecordState = string.Empty;
        }

        private Dictionary<string, object[]> CreateStateFromRecord(Form form, Record record)
        {
            Dictionary<string, object[]> strs = new Dictionary<string, object[]>();
            foreach (KeyValuePair<Guid, RecordField> recordField in record.RecordFields)
            {
                Field field = form.AllFields.FirstOrDefault<Field>((Field x) => x.Id == recordField.Value.FieldId);
                if (field != null)
                {
                    FieldType fieldTypeByField = _fieldTypeStorage.GetFieldTypeByField(field);
                    Guid id = field.Id;
                    strs.Add(id.ToString(), fieldTypeByField.ConvertFromRecord(field, recordField.Value.Values).ToArray<object>());
                }
            }
            return strs;
        }

        private Dictionary<string, object[]> ExtractAllPagesState(FormViewModel model, ControllerContext context, Form form)
        {
            Dictionary<string, object[]> strs;
            Guid id;
            object[] objArray;
            Dictionary<string, object[]> strs1 = RetrieveFormState(model);
            if (strs1 != null)
            {
                foreach (Field allField in form.AllFields)
                {
                    object[] array = new object[0];
                    object[] objArray1 = form.AllFields.First((Field f) => f.Id == allField.Id).Values == null ? new object[0] : form.AllFields.First((Field f) => f.Id == allField.Id).Values.ToArray();
                    if (context.HttpContext.Request.Form.AllKeys.Contains(allField.Id.ToString()))
                    {
                        NameValueCollection nameValueCollection = context.HttpContext.Request.Form;
                        id = allField.Id;
                        string[] values = nameValueCollection.GetValues(id.ToString());
                        bool flag = true;
                        if (values != null)
                        {
                            string[] strArrays = values;
                            int num = 0;
                            while (num < (int)strArrays.Length)
                            {
                                if (string.IsNullOrEmpty(strArrays[num]))
                                    num++;
                                else
                                {
                                    flag = false;
                                    break;
                                }
                            }
                            if (flag)
                                objArray = objArray1;
                            else
                                objArray = values;

                            array = objArray;
                        }
                    }
                    else if (!context.HttpContext.Request.Files.AllKeys.Contains(allField.Id.ToString()))  
                        array = objArray1;                   
                    else
                    {
                        Field field = form.AllFields.First<Field>((Field f) => f.Id == allField.Id);
                        FieldType fieldTypeByField = _fieldTypeStorage.GetFieldTypeByField(field);
                        array = fieldTypeByField.ProcessSubmittedValue(field, array, context.HttpContext).ToArray();
                    }
                    if (!strs1.ContainsKey(allField.Id.ToString()))
                    {
                        id = allField.Id;
                        strs1.Add(id.ToString(), array);
                    }
                    else
                    {
                        id = allField.Id;
                        strs1[id.ToString()] = array;
                    }
                }
                strs = strs1;
            }
            else
                strs = null;

            return strs;
        }

        private Dictionary<string, object[]> ExtractCurrentPageState(FormViewModel model, ControllerContext context, Form form)
        {
            Dictionary<string, object[]> strs = RetrieveFormState(model);
            if (strs != null)
            {
                foreach (FieldsetViewModel fieldset in model.CurrentPage.Fieldsets)
                {
                    foreach (FieldsetContainerViewModel container in fieldset.Containers)
                    {
                        foreach (FieldViewModel fieldViewModel in container.Fields)
                        {
                            object[] array = new object[0];
                            if (context.HttpContext.Request.Form.AllKeys.Contains<string>(fieldViewModel.Id))
                            {
                                object[] values = context.HttpContext.Request.Form.GetValues(fieldViewModel.Id);
                                array = values;
                            }
                            Field field1 = form.AllFields.First<Field>((Field field) => field.Id.ToString() == fieldViewModel.Id);
                            FieldType fieldTypeByField = _fieldTypeStorage.GetFieldTypeByField(field1);
                            array = fieldTypeByField.ProcessSubmittedValue(field1, array, context.HttpContext).ToArray<object>();
                            if (!strs.ContainsKey(fieldViewModel.Id)) 
                                strs.Add(fieldViewModel.Id, array);
                            else
                                strs[fieldViewModel.Id] = array;
                            
                        }
                    }
                }
            }
            return strs;
        }

        private void ExtractDataFromPages(FormViewModel model, Form form)
        {
            model.FormState = ExtractPagesState(model, ControllerContext, form);
            StoreFormState(model.FormState, model);
            ResumeFormState(model, model.FormState, false);
        }

        private Dictionary<string, object[]> ExtractPagesState(FormViewModel model, ControllerContext context, Form form)
        {
            Dictionary<string, object[]> strs = RetrieveFormState(model);
            if (strs != null)
            {
                foreach (PageViewModel page in model.Pages)
                {
                    foreach (FieldsetViewModel fieldset in page.Fieldsets)
                    {
                        foreach (FieldsetContainerViewModel container in fieldset.Containers)
                        {
                            foreach (FieldViewModel fieldViewModel in container.Fields)
                            {
                                object[] array = new object[0];
                                if (context.HttpContext.Request.Form.AllKeys.Contains<string>(fieldViewModel.Id))
                                {
                                    object[] values = context.HttpContext.Request.Form.GetValues(fieldViewModel.Id);
                                    array = values;
                                }
                                Field field1 = form.AllFields.First<Field>((Field field) => field.Id.ToString() == fieldViewModel.Id);
                                //FieldExtensions.PopulateDefaultValue(field1);
                                FieldType fieldTypeByField = _fieldTypeStorage.GetFieldTypeByField(field1);
                                array = fieldTypeByField.ProcessSubmittedValue(field1, array, context.HttpContext).ToArray<object>();
                                if (!strs.ContainsKey(fieldViewModel.Id))
                                    strs.Add(fieldViewModel.Id, array);
                                else
                                    strs[fieldViewModel.Id] = array;
                            }
                        }
                    }
                }
            }
            return strs;
        }

        private Form GetForm(Guid formId) => 
            _formStorage.GetForm(formId);
        

        private FormViewModel GetFormModel(Guid formId, Guid? recordId, string theme = "")
        {
            bool flag;
            IPublishedContent publishedContent;
            if (base.HttpContext.Items["pageElements"] == null)
            {
                UmbracoContext umbracoContext = _umbracoContextAccessor.UmbracoContext;
                if (umbracoContext != null)
                {
                    PublishedRequest publishedRequest = umbracoContext.PublishedRequest;
                    if (publishedRequest != null)
                    {
                        publishedContent = publishedRequest.PublishedContent;
                    }
                    else
                    {
                        publishedContent = null;
                    }
                }
                else
                {
                    publishedContent = null;
                }
                if (publishedContent != null)
                {
                    HttpContext.Items["pageElements"] = _pageService.GetPageElements();
                }
            }
            if (Session != null)
            {
                int currentMemberId = -1;
                if (Members.IsUmbracoMembershipProviderActive())
                {
                    try
                    {
                        currentMemberId = Members.GetCurrentMemberId();
                    }
                    catch (Exception exception1)
                    {
                        Exception exception = exception1;
                        Logger.Error(typeof(FormsController), "Can't get the current members Id", new object[] { exception });
                    }
                }
                if (currentMemberId <= -1)
                {
                    Session["ContourMemberKey"] = null;
                }
                else
                {
                    Session["ContourMemberKey"] = currentMemberId;
                }
            }
            FormViewModel formViewModel = RetrieveFormModel();
            if ((formViewModel == null ? false : formViewModel.FormId == formId))
            {
                if (!string.IsNullOrEmpty(theme))
                {
                    formViewModel.Theme = theme;
                }
                ResumeFormState(formViewModel, formViewModel.FormState, false);
            }
            else
            {
                Form form = GetForm(formId);
                formViewModel = new FormViewModel();
                if (!string.IsNullOrEmpty(theme))
                {
                    formViewModel.Theme = theme;
                }
                formViewModel.Build(form, _fieldTypeStorage, _fieldPreValueSourceService, _fieldPreValueSourceTypeService);
                PrePopulateForm(form, ControllerContext, formViewModel, null);
                ResumeFormState(formViewModel, formViewModel.FormState, false);
                if (formViewModel.IsFirstPage)
                {
                    ClearFormState(formViewModel);
                }
                if (!_configuration.AllowEditableFormSubmisisons)
                {
                    ExtractDataFromPages(formViewModel, form);
                }
                else
                {
                    if (!recordId.HasValue)
                    {
                        flag = false;
                    }
                    else
                    {
                        Guid? nullable = recordId;
                        Guid empty = Guid.Empty;
                        if (nullable.HasValue)
                        {
                            flag = (nullable.HasValue ? nullable.GetValueOrDefault() != empty : false);
                        }
                        else
                        {
                            flag = true;
                        }
                    }
                    if (!flag)
                    {
                        ExtractDataFromPages(formViewModel, form);
                    }
                    else
                    {
                        Record record = GetRecord(recordId.Value, form);
                        if (record != null)
                        {
                            PrePopulateForm(form, ControllerContext, formViewModel, record);
                            formViewModel.RecordId = record.UniqueId;
                            formViewModel.FormState = CreateStateFromRecord(form, record);
                            StoreFormState(formViewModel.FormState, formViewModel);
                            ResumeFormState(formViewModel, formViewModel.FormState, true);
                        }
                    }
                }
            }
            List<Guid> guids = new List<Guid>();
            if (TempData["UmbracoForms"] != null)
            {
                guids = (List<Guid>)TempData["UmbracoForms"];
            }
            if (!guids.Contains(formId))
            {
                guids.Add(formId);
            }
            //TempData.set_Item("UmbracoForms", guids);
            return formViewModel;
        }

        private Record GetRecord(Guid recordId, Form form) =>
            _recordStorage.GetRecordByUniqueId(recordId, form);
        

        private void GoBackward(FormViewModel model)
        {
            var formStep = model;
            formStep.FormStep -= 1;
            if (model.FormStep < 0)
                model.FormStep = 0;
            
        }

        private void GoForward(Form form, FormViewModel model, Dictionary<string, object[]> state)
        {
            var formStep = model;
            formStep.FormStep += 1;
            if (model.FormStep == form.Pages.Count())
                SubmitForm(form, model, state, ControllerContext);
            
        }

        [HttpPost]
        [ValidateFormsAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult HandleForm(FormViewModel model)
        {
            ActionResult umbracoPage;
            var form = GetForm(model.FormId);
            model.Build(form, _fieldTypeStorage, _fieldPreValueSourceService, _fieldPreValueSourceTypeService);
            if (!HoneyPotIsEmpty(model))
                model.SubmitHandled = true;   
            else
            {
                PrePopulateForm(form, ControllerContext, model, null);
                model.FormState = (FormPrePopulate == null ? ExtractCurrentPageState(model, ControllerContext, form) : ExtractAllPagesState(model, ControllerContext, form));
                StoreFormState(model.FormState, model);
                ResumeFormState(model, model.FormState, false);
                if ((!string.IsNullOrEmpty(Request["__prev"]) || !string.IsNullOrEmpty(Request["PreviousClicked"]) ? model.FormStep <= 0 : true))
                {
                    ValidateFormState(model, form, ControllerContext.HttpContext);
                    if (ModelState.IsValid)
                    {
                        GoForward(form, model, model.FormState);
                    }
                }
                else
                    GoBackward(model);
                
                model.IsFirstPage = model.FormStep == 0;
                model.IsLastPage = model.FormStep == form.Pages.Count - 1;
            }
            OnFormHandled(form, model);
            StoreFormModel(model);
            //return CurrentUmbracoPage();
            if ((!model.SubmitHandled ? true : form.GoToPageOnSubmit <= 0))
                umbracoPage = CurrentUmbracoPage();          
            else
            {
                ClearFormModel();
                ClearFormState(model);
                umbracoPage = RedirectToUmbracoPage(form.GoToPageOnSubmit);
            }
            return umbracoPage;
        }

        private bool HoneyPotIsEmpty(FormViewModel model)
        {
            var request = Request;
            Guid formId = model.FormId;
            return string.IsNullOrEmpty(request[formId.ToString().Replace("-", string.Empty)]);
        }

        protected virtual void OnFormHandled(Form form, FormViewModel model)
        {
        }

        private static void PopulateFieldValues(FormViewModel model, Form form)
        {
            object[] objArray;
            foreach (Field allField in form.AllFields)
            {
                Dictionary<string, object[]> formState = model.FormState;
                Guid id = allField.Id;
                formState.TryGetValue(id.ToString(), out objArray);
                allField.Values = (objArray != null ? objArray.ToList() : new List<object>());
            }
        }
        private void PrePopulateForm(Form form, ControllerContext context, FormViewModel formViewModel, Record record = null)
        {
            Guid id;
            Dictionary<string, object[]> strs = RetrieveFormState(formViewModel);
            object[] value = new object[0];
            if (FormPrePopulate != null)
            {
                foreach (Field allField in form.AllFields)
                {
                    if (!context.HttpContext.Request.Form.AllKeys.Contains(allField.Id.ToString()))
                    {
                        KeyValuePair<string, object[]> keyValuePair = strs.FirstOrDefault<KeyValuePair<string, object[]>>((KeyValuePair<string, object[]> v) => v.Key == allField.Id.ToString());
                        value = keyValuePair.Value;
                        if (value != null)
                        {
                            object[] objArray = value;
                            for (int i = 0; i < (int)objArray.Length; i++)
                            {
                                object obj = objArray[i];
                                if (allField.Values == null)                               
                                    allField.Values.Add(new List<object>());                              
                                else if (allField.Settings.Keys.Contains("DefaultValue"))
                                    allField.Values.Clear();
                                
                                if (obj.Equals(string.Empty))                                
                                    allField.Values.Clear();                                
                                else                               
                                    allField.Values.Add(obj);
                                
                            }
                        }
                        else 
                            continue;
                        
                    }
                    else
                    {
                        var nameValueCollection = context.HttpContext.Request.Form;
                        id = allField.Id;
                        value = nameValueCollection.GetValues(id.ToString());
                        if (allField.Values == null) 
                            allField.Values.Add(new List<object>());                      
                        else if (allField.Settings.Keys.Contains("DefaultValue"))         
                            allField.Values.Clear();
                        

                        object[] objArray1 = value;
                        for (int j = 0; j < objArray1.Length; j++)
                        {
                            object obj1 = objArray1[j];
                            if (obj1.Equals(string.Empty))  
                                allField.Values.Clear();                      
                            else                            
                                allField.Values.Add(obj1);                            
                        }
                    }
                    if (!strs.ContainsKey(allField.Id.ToString()))
                    {
                        id = allField.Id;
                        strs.Add(id.ToString(), value);
                    }
                    else
                    {
                        id = allField.Id;
                        strs[id.ToString()] = value;
                    }
                }
                bool? nullable = null;
                using (IScope scope = _scopeProvider.CreateScope(IsolationLevel.Unspecified, 0, null, nullable, false, true))
                {
                    scope.Events.Dispatch(FormPrePopulate, this, new FormEventArgs(form), "PrePopulatingForm");
                }
                if (record != null)
                {
                    foreach (Field field in form.AllFields)
                    {
                        object[] array = new object[0];
                        FieldType fieldTypeByField = _fieldTypeStorage.GetFieldTypeByField(field);
                        array = fieldTypeByField.ConvertToRecord(field, array, ControllerContext.HttpContext).ToArray<object>();
                        if (record.GetRecordField(field.Id) == null)
                        {
                            RecordField recordField = new RecordField(field);
                            recordField.Values.AddRange(array);
                            record.RecordFields.Add(field.Id, recordField);
                        }
                    }
                    foreach (KeyValuePair<Guid, RecordField> recordField1 in record.RecordFields)
                    {
                        foreach (Field allField1 in form.AllFields)
                        {
                            if (allField1.Id == recordField1.Value.FieldId)
                            {
                                if (allField1.Values != null)  
                                    recordField1.Value.Values.Add(allField1.Values);                               
                            }
                        }
                    }
                }
            }
        }

        //[ChildActionOnly]
        //public ActionResult Render(Guid formId, Guid? recordId = null, string view = "", string mode = "full")
        //{
        //    FormViewModel formModel = GetFormModel(formId, recordId, "");
        //    formModel.RenderMode = mode;
        //    if (File.Exists(base.get_Server().MapPath(string.Format("{0}/{1}/Form.cshtml", Constants.System.ViewsPath, formModel.FormId))))
        //    {
        //        view = Path.Combine(Constants.System.ViewsPath, string.Format("{0}/Form.cshtml", formModel.FormId));
        //    }
        //    else if (string.IsNullOrEmpty(view))
        //    {
        //        view = Path.Combine(Constants.System.ViewsPath, "Form.cshtml");
        //    }
        //    else if ((view.StartsWith("~") ? false : !view.StartsWith("/")))
        //    {
        //        view = Path.Combine(Constants.System.ViewsPath, view);
        //    }
        //    return PartialView(view, formModel);
        //}

        [ChildActionOnly]
        public ActionResult RenderForm(Guid formId, Guid? recordId = null, string theme = "", bool includeScripts = true)
        {
            FormViewModel formModel = GetFormModel(formId, recordId, theme);
            formModel.RenderScriptFiles = includeScripts;
            return PartialView(FormThemeResolver.GetFormRender(formModel), formModel);
        }

        [ChildActionOnly]
        public ActionResult RenderFormScripts(Guid formId, string theme = "")
        {
            FormViewModel formModel = GetFormModel(formId, null, theme);
            return PartialView(FormThemeResolver.GetScriptView(formModel), formModel);
        }

        private void ResumeFormState(FormViewModel model, Dictionary<string, object[]> state, bool editSubmission = false)
        {
            if (state != null)
            {
                foreach (PageViewModel page in model.Pages)
                {
                    foreach (FieldsetViewModel fieldset in page.Fieldsets)
                    {
                        foreach (FieldsetContainerViewModel container in fieldset.Containers)
                        {
                            foreach (FieldViewModel field in container.Fields)
                            {
                                if (editSubmission)
                                {
                                    if (field.FieldType.Id == Guid.Parse("A72C9DF9-3847-47CF-AFB8-B86773FD12CD"))
                                    {
                                        FieldType item = _fieldCollection[Guid.Parse("DA206CAE-1C52-434E-B21A-4A7C198AF877")];
                                        field.FieldType = item;
                                        field.HideLabel = true;
                                    }
                                }
                                if (state.ContainsKey(field.Id))
                                    field.Values = state[field.Id];
                               
                            }
                        }
                    }
                }
            }
        }

        private FormViewModel RetrieveFormModel()
        {
            FormViewModel item;
            if (TempData.ContainsKey("umbracoformsform"))
                item = TempData["umbracoformsform"] as FormViewModel;           
            else         
                item = null;
            
            return item;
        }

        private Dictionary<string, object[]> RetrieveFormState(FormViewModel model)
        {
            Dictionary<string, object[]> strs;
            if (!string.IsNullOrEmpty(model.RecordState))
            {
                var str = Base64Decode(model.RecordState);
                strs = JsonConvert.DeserializeObject<Dictionary<string, object[]>>(str.DecryptWithMachineKey());
            }
            else
                strs = new Dictionary<string, object[]>();
            
            return strs;
        }

        private void StoreFormModel(FormViewModel model)
        {
            TempData["umbracoformsform"] = model;
        }

        private void StoreFormState(Dictionary<string, object[]> state, FormViewModel model)
        {
            string str = JsonConvert.SerializeObject(state);
            model.RecordState = Base64Encode(str.EncryptWithMachineKey());
        }

        private void SubmitForm(Form form, FormViewModel model, Dictionary<string, object[]> state, ControllerContext context)
        {
            Guid id;
            bool flag;
            string str;
            using (DisposableTimer disposableTimer = Current.ProfilingLogger.DebugDuration<FormsController>(string.Format("Umbraco Forms: Submitting Form '{0}' with id '{1}'", form.Name, form.Id)))
            {
                model.SubmitHandled = true;
                Record record = new Record();
                if (model.RecordId != Guid.Empty)
                {
                    record = GetRecord(model.RecordId, form);
                }
                record.Form = form.Id;
                record.State = FormState.Submitted;
                record.UmbracoPageId = CurrentPage.Id;
                record.IP = HttpContext.Request.UserHostAddress;

                bool isAuthenticated = false;
                string name = null;
                var hUser = HttpContext.User;
                if ((HttpContext == null || hUser == null ? false : hUser.Identity != null))
                {
                    isAuthenticated = hUser.Identity.IsAuthenticated;
                    name = hUser.Identity.Name;
                }
                if ((!isAuthenticated ? false : !string.IsNullOrEmpty(name)))
                {
                    var user = Membership.GetUser(name);
                    if (user != null)
                    {
                        var record1 = record;
                        object providerUserKey = user.ProviderUserKey;
                        if (providerUserKey != null)
                            str = providerUserKey.ToString();                  
                        else                     
                            str = null;
                        
                        record1.MemberKey = str;
                    }
                }
                foreach (var allField in form.AllFields)
                {
                    object[] item = new object[0];
                    if (state == null)
                        flag = false;                    
                    else
                    {
                        id = allField.Id;
                        flag = state.ContainsKey(id.ToString());
                    }
                    if (flag)
                    {
                        id = allField.Id;
                        item = state[id.ToString()];
                    }
                    var fieldTypeByField = _fieldTypeStorage.GetFieldTypeByField(allField);
                    item = fieldTypeByField.ConvertToRecord(allField, item, context.HttpContext).ToArray<object>();
                    if (record.GetRecordField(allField.Id) == null)
                    {
                        var recordField = new RecordField(allField);
                        recordField.Values.AddRange(item);
                        record.RecordFields.Add(allField.Id, recordField);
                    }
                    else
                    {
                        var recordFields = record.GetRecordField(allField.Id).Values;
                        recordFields.Clear();
                        recordFields.AddRange(item);
                    }
                }
                ClearFormState(model);
                _recordService.Submit(record, form);
                _recordService.AddRecordIdToTempData(record, ControllerContext);
            }
        }

        private void ValidateFormState(FormViewModel model, Form form, HttpContextBase context)
        {
            PopulateFieldValues(model, form);
            Dictionary<Guid, string> dictionary = form.AllFields.ToDictionary<Field, Guid, string>((Field f) => f.Id, (Field f) => string.Join<object>(", ", f.Values ?? new List<object>()));
            foreach (FieldSet fieldSet in form.Pages[model.FormStep].FieldSets)
            {
                if ((fieldSet.Condition == null ? false : fieldSet.Condition.Enabled))
                {
                    if (!FieldConditionEvaluation.IsVisible(fieldSet.Condition, form, dictionary))
                        continue;                   
                }
                foreach (Field field in fieldSet.Containers.SelectMany((FieldsetContainer c) => c.Fields))
                {
                    var nameValueCollection = context.Request.Form;
                    var id = field.Id;
                    string[] values = nameValueCollection.GetValues(id.ToString()) ?? Array.Empty<string>();
                    var fieldTypeByField = _fieldTypeStorage.GetFieldTypeByField(field);
                    object obj = fieldTypeByField.ValidateField(form, field, values, HttpContext, _formStorage);
                    if (obj == null)
                        obj = new string[0];                    
                    foreach (string str in (IEnumerable<string>)obj)
                    {
                        string str1 = str;
                        if (string.IsNullOrWhiteSpace(str))
                        {
                            //str1 = StringExtensions.ParsePlaceHolders( form.InvalidErrorMessage ?? string.Empty,field.Caption);
                            str1 = field.Caption;
                        }
                        var modelState = ModelState;
                        id = field.Id;
                        modelState.AddModelError(id.ToString(), str1);
                    }
                }
            }
            FormValidate?.Invoke(this, new FormValidationEventArgs(form));
        }

        public static event EventHandler<FormEventArgs> FormPrePopulate;

        public static event EventHandler<FormValidationEventArgs> FormValidate;
    }
}