﻿/* Copyright © 2020 Lee Kelleher.
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;

namespace Umbraco.Community.Contentment.DataEditors
{
    [Obsolete("This interface has been deprecated. Please used `IDataSourceValueConverter` instead.")]
    public interface IDataListSourceValueConverter : IDataSourceValueConverter, IDataListSource
    { }
}
