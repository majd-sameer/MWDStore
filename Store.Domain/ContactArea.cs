using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ContactArea
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public ICollection<Contact> Contacts { get; set; } = [];
}

