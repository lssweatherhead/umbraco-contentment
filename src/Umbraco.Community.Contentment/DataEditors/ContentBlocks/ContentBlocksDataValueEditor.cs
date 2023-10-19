﻿/* Copyright © 2014 Umbrella Inc, Our Umbraco and other contributors.
 * This Source Code has been derived from Nested Content.
 * https://github.com/umco/umbraco-nested-content/blob/0.5.0/src/Our.Umbraco.NestedContent/PropertyEditors/NestedContentPropertyEditor.cs
 * Including derivations made in Umbraco CMS for v8. Copyright © 2013-present Umbraco.
 * https://github.com/umbraco/Umbraco-CMS/blob/release-8.4.0/src/Umbraco.Web/PropertyEditors/NestedContentPropertyEditor.cs
 * Modified under the permissions of the MIT License.
 * Modifications are licensed under the Mozilla Public License.
 * Copyright © 2019 Lee Kelleher.
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#if NET472
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Editors;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using UmbConstants = Umbraco.Core.Constants;
#else
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Editors;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;
using UmbConstants = Umbraco.Cms.Core.Constants;
#endif

namespace Umbraco.Community.Contentment.DataEditors
{
    internal sealed class ContentBlocksDataValueEditor : DataValueEditor, IDataValueReference
    {
        private readonly IDataTypeService _dataTypeService;
        private readonly Lazy<Dictionary<Guid, IContentType>> _elementTypes;
        private readonly PropertyEditorCollection _propertyEditors;

#if NET472
        public ContentBlocksDataValueEditor(
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService,
            PropertyEditorCollection propertyEditors)
            : base()
#else
        public ContentBlocksDataValueEditor(
            IContentTypeService contentTypeService,
            PropertyEditorCollection propertyEditors,
            IDataTypeService dataTypeService,
            ILocalizedTextService localizedTextService,
            IShortStringHelper shortStringHelper,
            IJsonSerializer jsonSerializer,
            IPropertyValidationService propertyValidationService)
            : base(localizedTextService, shortStringHelper, jsonSerializer)
#endif
        {
            _dataTypeService = dataTypeService;
            _elementTypes = new Lazy<Dictionary<Guid, IContentType>>(() => contentTypeService.GetAllElementTypes().ToDictionary(x => x.Key));
            _propertyEditors = propertyEditors;

#if NET472 == false
            Validators.Add(new ContentBlocksValueValidator(_elementTypes, propertyValidationService));
#endif
        }


#if NET472
        public override object ToEditor(Property property, IDataTypeService dataTypeService, string culture = null, string segment = null)
        {
            var value = base.ToEditor(property, dataTypeService, culture, segment)?.ToString();
#else
        public override object ToEditor(IProperty property, string culture = null, string segment = null)
        {
            var value = base.ToEditor(property, culture, segment)?.ToString();
#endif
            if (string.IsNullOrWhiteSpace(value) == false)
            {
                var blocks = JsonConvert.DeserializeObject<IEnumerable<ContentBlock>>(value);
                if (blocks?.Any() == true)
                {
                    foreach (var block in blocks)
                    {
                        if (block != null &&
                            _elementTypes.Value.TryGetValue(block.ElementType, out var elementType) == true)
                        {
                            if (block.Udi == null)
                            {
                                block.Udi = Udi.Create(UmbConstants.UdiEntityType.Element, block.Key);
                            }

                            foreach (var propertyType in elementType.CompositionPropertyTypes)
                            {
                                if (block.Value.TryGetValue(propertyType.Alias, out var blockPropertyValue) == true)
                                {
                                    propertyType.Variations = ContentVariation.Nothing;

                                    var fakeProperty = new Property(propertyType);
                                    fakeProperty.SetValue(blockPropertyValue);

                                    if (_propertyEditors.TryGet(propertyType.PropertyEditorAlias, out var propertyEditor) == true)
                                    {
                                        var convertedValue = propertyEditor.GetValueEditor()?.ToEditor(fakeProperty, _dataTypeService);

                                        block.Value[propertyType.Alias] = convertedValue != null
                                            ? JToken.FromObject(convertedValue)
                                            : null;
                                    }
                                    else
                                    {
                                        block.Value[propertyType.Alias] = fakeProperty.GetValue()?.ToString();
                                    }
                                }
                            }
                        }
                    }

                    return blocks;
                }
            }

            return Array.Empty<object>();
        }

        public override object FromEditor(ContentPropertyData editorValue, object currentValue)
        {
            var value = editorValue?.Value?.ToString();
            if (string.IsNullOrWhiteSpace(value) == false)
            {
                var blocks = JsonConvert.DeserializeObject<IEnumerable<ContentBlock>>(value);
                if (blocks?.Any() == true)
                {
                    foreach (var block in blocks)
                    {
                        if (block != null &&
                            _elementTypes.Value.TryGetValue(block.ElementType, out var elementType) == true)
                        {
                            foreach (var propertyType in elementType.CompositionPropertyTypes)
                            {
                                if (block.Value.TryGetValue(propertyType.Alias, out var blockPropertyValue) == true &&
                                    _propertyEditors.TryGet(propertyType.PropertyEditorAlias, out var propertyEditor) == true)
                                {
                                    var configuration = _dataTypeService.GetDataType(propertyType.DataTypeId).Configuration;
                                    var contentPropertyData = new ContentPropertyData(blockPropertyValue, configuration)
                                    {
                                        ContentKey = block.Key,
                                        PropertyTypeKey = propertyType.Key,
                                        Files = Array.Empty<ContentPropertyFile>()
                                    };

                                    var convertedValue = propertyEditor.GetValueEditor(configuration)?.FromEditor(contentPropertyData, blockPropertyValue);

                                    block.Value[propertyType.Alias] = convertedValue != null
                                        ? JToken.FromObject(convertedValue)
                                        : null;
                                }
                            }
                        }
                    }

                    return JsonConvert.SerializeObject(blocks, Formatting.None);
                }
            }

            return base.FromEditor(editorValue, currentValue);
        }

        public IEnumerable<UmbracoEntityReference> GetReferences(object value)
        {
            if (value is string str && string.IsNullOrWhiteSpace(str) == false && str.DetectIsJson() == true)
            {
                var blocks = JsonConvert.DeserializeObject<IEnumerable<ContentBlock>>(str);
                if (blocks?.Any() == true)
                {
                    foreach (var block in blocks)
                    {
                        if (block != null)
                        {
                            if (_elementTypes.Value.TryGetValue(block.ElementType, out var elementType) == true)
                            {
                                foreach (var propertyType in elementType.CompositionPropertyTypes)
                                {
                                    if (block.Value.TryGetValue(propertyType.Alias, out var bpv) == true &&
                                        _propertyEditors.TryGet(propertyType.PropertyEditorAlias, out var editor) == true &&
                                        editor?.GetValueEditor() is IDataValueReference dvr)
                                    {
                                        foreach (var reference in dvr.GetReferences(bpv))
                                        {
                                            yield return reference;
                                        }
                                    }
                                }
                            }
                            else if (block.Udi?.EntityType == UmbConstants.UdiEntityType.Document)
                            {
                                yield return new UmbracoEntityReference(block.Udi);
                            }
                        }
                    }
                }
            }
        }
    }
}
