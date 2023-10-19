﻿/* Copyright © 2019 Lee Kelleher.
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

angular.module("umbraco").controller("Umbraco.Community.Contentment.Overlays.ContentBlocks.Controller", [
    "$scope",
    "$http",
    "blueprintConfig",
    "clipboardService",
    "contentResource",
    "editorService",
    "editorState",
    "umbRequestHelper",
    function ($scope, $http, blueprintConfig, clipboardService, contentResource, editorService, editorState, umbRequestHelper) {

        // console.log("content-blocks-overlay.model", $scope.model, blueprintConfig);

        var defaultConfig = {
            elementType: null,
            elementTypes: [],
            reusableItems: [],
            enableFilter: true,
            currentPage: null,
            currentPageId: -2,
        };
        var config = Object.assign({}, defaultConfig, $scope.model.config);

        var vm = this;

        function init() {

            vm.submit = submit;
            vm.close = close;

            // NOTE: Fixes https://github.com/leekelleher/umbraco-contentment/issues/250
            vm.contentNodeModel = config.currentPage || { variants: [] };

            if (config.elementType && $scope.model.value) {

                edit(config.elementType, $scope.model.value);

            } else {

                vm.mode = "select";
                vm.items = config.elementTypes;
                vm.reusableItems = config.reusableItems;
                vm.selectedElementType = null;

                // NOTE: Corrected `retriveDataOfType` typo, but kept backwards-compatibility for v8.17.x.
                // ref: https://github.com/umbraco/Umbraco-CMS/pull/11027
                vm.clipboardItems = typeof clipboardService.retrieveDataOfType === "function"
                    ? clipboardService.retrieveDataOfType("elementType", config.elementTypes.map(item => item.alias))
                    : clipboardService.retriveDataOfType("elementType", config.elementTypes.map(item => item.alias));

                if (config.elementTypes.length + vm.clipboardItems.length + vm.reusableItems.length > 1) {

                    vm.title = "Add content";
                    vm.description = "Select a content type...";
                    vm.icon = "icon-page-add";
                    vm.selectBlueprint = false;
                    vm.enableFilter = Object.toBoolean(config.enableFilter);

                    vm.select = select;
                    vm.paste = paste;
                    vm.tree = tree;

                    vm.clearClipboard = clearClipboard;
                    vm.prompt = false;
                    vm.showPrompt = showPrompt;
                    vm.hidePrompt = hidePrompt;

                } else if (config.elementTypes.length === 1) {

                    select(config.elementTypes[0]);

                } else if (config.reusableItems.length === 1) {

                    tree(config.reusableItems[0])

                }
            }
        };

        function clearClipboard() {
            vm.clipboardItems = [];
            clipboardService.clearEntriesOfType("elementType", config.elementTypes.map(item => item.alias));
        };

        function showPrompt() {
            vm.prompt = true;
        };

        function hidePrompt() {
            vm.prompt = false;
        };

        function select(elementType) {
            if (elementType.blueprints && elementType.blueprints.length > 0) {
                if (elementType.blueprints.length === 1 && blueprintConfig.skipSelect) {
                    create(elementType, elementType.blueprints[0]);
                }
                else {
                    vm.title = "Add content";
                    vm.description = "Select a content blueprint...";
                    vm.icon = "icon-blueprint";
                    vm.selectBlueprint = true;
                    vm.selectedElementType = elementType;
                    vm.blueprintAllowBlank = blueprintConfig.allowBlank;
                    vm.create = create;
                }
            } else {
                create(elementType);
            }
        };

        function create(elementType, blueprint) {

            $scope.model.size = elementType.overlaySize;

            vm.mode = "edit";
            vm.loading = true;

            vm.title = "Edit content";
            vm.description = elementType.name;
            vm.icon = elementType.icon;

            vm.content = {
                elementType: elementType.key,
                key: String.CreateGuid()
            };

            // TODO: [v9] [LK] Review this, get error with blueprint API request, 404.
            // "Failed to retrieve blueprint for id 1082"
            // e.g. /umbraco/backoffice/umbracoapi/content/GetEmpty?blueprintId=1082&parentId=1076
            var getScaffold = blueprint && blueprint.id > 0
                ? contentResource.getBlueprintScaffold(config.currentPageId, blueprint.id)
                : contentResource.getScaffold(config.currentPageId, elementType.alias);

            getScaffold.then(data => {
                Object.assign(vm.content, data.variants[0]);
                vm.loading = false;
            });

        };

        function paste(bloat) {

            var elementType = config.elementTypes.find(x => x.alias === bloat.contentTypeAlias);

            $scope.model.size = elementType.overlaySize;

            var item = {
                elementType: elementType.key,
                key: String.CreateGuid(),
                value: {}
            };

            // NOTE: De-bloat the copied value (so much bloat from NC) ¯\_(ツ)_/¯
            if (bloat.variants.length > 0) {
                for (var t = 0; t < bloat.variants[0].tabs.length; t++) {
                    var tab = bloat.variants[0].tabs[t];
                    for (var p = 0; p < tab.properties.length; p++) {
                        var property = tab.properties[p];
                        if (typeof property.value !== "function") {
                            // NOTE: Gah, NC adds `propertyAlias` property! ¯\_(ツ)_/¯
                            item.value[property.propertyAlias] = property.value;
                        }
                    }
                }
            }

            edit(elementType, item);
        };

        function edit(elementType, element) {

            vm.mode = "edit";
            vm.loading = true;

            vm.title = "Edit content";
            vm.description = elementType.name;
            vm.icon = elementType.icon;

            vm.content = {
                elementType: elementType.key,
                key: element.key
            };

            contentResource.getScaffold(config.currentPageId, elementType.alias).then(data => {

                if (element.value) {
                    for (var t = 0; t < data.variants[0].tabs.length; t++) {
                        var tab = data.variants[0].tabs[t];
                        for (var p = 0; p < tab.properties.length; p++) {
                            var property = tab.properties[p];
                            if (element.value.hasOwnProperty(property.alias)) {
                                property.value = element.value[property.alias];
                            }
                        }
                    }
                }

                Object.assign(vm.content, data.variants[0]);
                vm.loading = false;
            });

        };

        function tree(item) {

            //console.log("content-blocks-overlay.tree", item);

            editorService.contentPicker({
                multiPicker: false,
                size: "small",
                startNodeId: item.parentNode,
                currentNode: editorState.getCurrent(),
                submit: function (model) {

                    var item = model.selection[0];

                    //console.log("content-blocks-overlay.contentPicker.submit", item);

                    umbRequestHelper.resourcePromise(
                        $http.get(
                            "backoffice/Contentment/ContentBlocksApi/GetContentTypeByAlias",
                            { params: { alias: item.metaData.ContentTypeAlias } }
                        ),
                        "Failed to retrieve content type by alias")
                        .then(result => {
                            if (result && result.contentTypeKey) {

                                //console.log("content-blocks-overlay.contentResource.getScaffold", result);

                                $scope.model.submit({
                                    elementType: result.contentTypeKey,
                                    key: item.key,
                                    udi: item.udi,
                                });

                            }
                        });

                    editorService.close();
                },
                close: function () {
                    editorService.close();
                }
            });

        };

        function submit() {

            if ($scope.model.submit) {

                $scope.$broadcast("formSubmitting", { scope: $scope });

                var item = {
                    elementType: vm.content.elementType,
                    key: vm.content.key,
                    udi: "umb://element/" + vm.content.key.replaceAll("-", ""),
                    value: {},
                };

                if (vm.content.tabs.length > 0) {
                    for (var t = 0; t < vm.content.tabs.length; t++) {
                        var tab = vm.content.tabs[t];
                        for (var p = 0; p < tab.properties.length; p++) {
                            var property = tab.properties[p];
                            if (typeof property.value !== "function") {
                                item.value[property.alias] = property.value;
                            }
                        }
                    }
                }

                $scope.model.submit(item);
            }
        };

        function close() {
            if ($scope.model.close) {
                $scope.model.close();
            }
        };

        init();
    }
]);
