using System;
using System.Collections.Generic;

namespace Store.Domain;

public class AppSetting
{
    public string Id { get; set; } = null!;

    public string? Value { get; set; }

    public string? Module { get; set; }

    public bool IsVisibleInCommonSettingPage { get; set; }
}

