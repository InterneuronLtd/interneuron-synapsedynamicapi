 //Interneuron synapse

//Copyright(C) 2024 Interneuron Limited

//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

//See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program.If not, see<http://www.gnu.org/licenses/>.
﻿using Interneuron.Common.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SynapseDynamicAPI.Infrastructure.Filters
{
    public class GlobalFilter : IAsyncActionFilter
    {
        private HashSet<string> _fieldsToDescope = new();
        private readonly IConfiguration _configuration;

        public GlobalFilter(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            try
            {
                ManipulateRequestData(context);
            }
            catch { }//suppressed intentionally

            await next();
        }

        private void LoadFieldsToDescopeFromConfig()
        {
            var descopedFieldsConfigured = _configuration["SynapseCore:Settings:DescopedFieldsForUpsert"] ?? "";
            descopedFieldsConfigured.Split("|").Each(rec => { if (rec.IsNotEmpty() && !_fieldsToDescope.Contains(rec)) _fieldsToDescope.Add(rec); });
        }

        /// <summary>
        /// This method removes the configured system generated fields from the posted input data
        /// Input: List<Dictionary<string, object>>, object can be JObject or JArray and if JArray is Array of JObject
        /// </summary>
        /// <param name="context"></param>
        private void ManipulateRequestData(ActionExecutingContext context)
        {
            if (context == null || context.HttpContext == null || context.HttpContext.Request == null || context.HttpContext.Request.Path == null) return;

            switch (context.HttpContext.Request.Path.Value)
            {
                case string valInTran when valInTran is not null && valInTran.Contains("/PostObjectsInTransaction", StringComparison.OrdinalIgnoreCase):
                    ManipulatePostObjectsInTransactionData(context);
                    break;
                case string valArr when valArr is not null && valArr.Contains("/PostObjectArray", StringComparison.OrdinalIgnoreCase):
                    ManipulatePostObjectArray(context);
                    break;
                case string valObj when valObj is not null && valObj.Contains("/PostObject", StringComparison.OrdinalIgnoreCase):
                    ManipulatePostObject(context);
                    break;
                default: break;
            }
        }

        private void ManipulatePostObject(ActionExecutingContext context)
        {
            var data = context.ActionArguments["data"];

            LoadFieldsToDescopeFromConfig();

            if (data == null || !_fieldsToDescope.IsCollectionValid()) return;

            var dataAsLkp = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString());

            if (!dataAsLkp.IsCollectionValid()) return;

            var entityData = JsonConvert.SerializeObject(dataAsLkp);

            var entityDataAsObj = JsonConvert.DeserializeObject(entityData);

            var modifiedObj = HandleJObject<Dictionary<string, object>>(entityDataAsObj);

            context.ActionArguments["data"] = JsonConvert.SerializeObject(modifiedObj);
        }

        private void ManipulatePostObjectArray(ActionExecutingContext context)
        {
            var data = context.ActionArguments["data"];

            LoadFieldsToDescopeFromConfig();

            if (data == null || !_fieldsToDescope.IsCollectionValid()) return;

            var dataAsList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(data.ToString());

            if (!dataAsList.IsCollectionValid()) return;

            var morphedDataAsList = new List<Dictionary<string, object>>();

            foreach (var entityOuter in dataAsList)
            {
                var entityData = JsonConvert.SerializeObject(entityOuter);
                var entityDataAsObj = JsonConvert.DeserializeObject(entityData);

                var modifiedObj = HandleJObject<Dictionary<string, object>>(entityDataAsObj);
                morphedDataAsList.Add(modifiedObj);
            }
            context.ActionArguments["data"] = JsonConvert.SerializeObject(morphedDataAsList);
        }

        private void ManipulatePostObjectsInTransactionData(ActionExecutingContext context)
        {
            var data = context.ActionArguments["data"];

            LoadFieldsToDescopeFromConfig();

            if (data == null || !_fieldsToDescope.IsCollectionValid()) return;

            var dataAsList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(data.ToString());

            if (!dataAsList.IsCollectionValid()) return;

            var morphedDataAsList = new List<Dictionary<string, object>>();

            dataAsList.Each(entityOuter =>
            {
                var morphedOuterDataAsDict = new Dictionary<string, object>();
                entityOuter.Each(entity =>
                {
                    morphedOuterDataAsDict.Add(entity.Key, entity.Value);

                    if (entity.Value.GetType().Name == "JArray")
                    {
                        var modifiedArr = HandleJArray<object?>(entity.Value);
                        morphedOuterDataAsDict[entity.Key] = modifiedArr;
                    }
                    else if (entity.Value.GetType().Name == "JObject")
                    {
                        var modifiedObj = HandleJObject<object?>(entity.Value);
                        morphedOuterDataAsDict[entity.Key] = modifiedObj;
                    }
                });

                morphedDataAsList.Add(morphedOuterDataAsDict);
            });

            context.ActionArguments["data"] = JsonConvert.SerializeObject(morphedDataAsList);
        }

        private T HandleJArray<T>(object value)
        {
            var entityData = JsonConvert.SerializeObject(value);

            var arrayOfEntities = JsonConvert.DeserializeObject<List<object>>(entityData);

            List<object> items = new();

            arrayOfEntities.Each(item =>
            {
                var newObject = HandleJObject<object?>(item);
                items.Add(newObject);
            });
            var itemsSerialized = JsonConvert.SerializeObject(items);
            return JsonConvert.DeserializeObject<T>(itemsSerialized);
        }

        private T HandleJObject<T>(object value)
        {
            var entityData = JsonConvert.SerializeObject(value);

            var dataDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(entityData);
            var dataDictAsCloned = JsonConvert.DeserializeObject<Dictionary<string, object>>(entityData);

            //Defenced well, above
            _fieldsToDescope.Each(field =>
            {
                if (dataDict.ContainsKey(field))
                    dataDictAsCloned.Remove(field);
            });

            entityData = JsonConvert.SerializeObject(dataDictAsCloned);

            return JsonConvert.DeserializeObject<T>(entityData);
        }
    }
}
