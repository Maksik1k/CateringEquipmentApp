using CateringEquipmentApp.Models;

namespace CateringEquipmentApp.Services;

public sealed class PermissionService
{
    private readonly Dictionary<string, Dictionary<string, SectionPermissions>> _rolePermissions;

    public PermissionService()
    {
        _rolePermissions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Администратор"] = BuildAdminAccess(),
            ["Управляющий"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["equipment"] = FullCrudWithAssign(),
                ["history"] = ViewOnly(),
                ["requests"] = FullCrudWithAssign(),
                ["repairs"] = CrudNoAssign(),
                ["replacements"] = CrudNoAssign(),
                ["maintenance"] = FullCrudWithAssign(),
                ["sanitation"] = CrudNoAssign(),
                ["appeals"] = FullCrudWithAssign(),
                ["notifications"] = ViewOnly(),
                ["reports"] = ViewOnly()
            },
            ["Повар"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["equipment"] = ViewOnly(),
                ["history"] = ViewOnly(),
                ["requests"] = new SectionPermissions { CanView = true, CanAdd = true },
                ["sanitation"] = new SectionPermissions { CanView = true, CanAdd = true, CanEdit = true },
                ["appeals"] = new SectionPermissions { CanView = true, CanAdd = true },
                ["notifications"] = ViewOnly(),
                ["reports"] = ViewOnly()
            },
            ["Кладовщик"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["equipment"] = ViewOnly(),
                ["repairs"] = ViewOnly(),
                ["replacements"] = new SectionPermissions { CanView = true, CanAdd = true, CanEdit = true, CanDelete = true },
                ["maintenance"] = ViewOnly(),
                ["appeals"] = new SectionPermissions { CanView = true, CanAdd = true },
                ["reports"] = ViewOnly()
            },
            ["Мастер сервисной организации"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["equipment"] = ViewOnly(),
                ["history"] = ViewOnly(),
                ["requests"] = new SectionPermissions { CanView = true, CanAssign = true },
                ["repairs"] = CrudNoAssign(),
                ["notifications"] = ViewOnly()
            }
        };
    }

    public SectionPermissions GetPermissions(string roleName, string sectionKey)
    {
        if (_rolePermissions.TryGetValue(roleName, out var sections) &&
            sections.TryGetValue(sectionKey, out var permissions))
        {
            return permissions;
        }

        return new SectionPermissions();
    }

    private static Dictionary<string, SectionPermissions> BuildAdminAccess()
    {
        return new(StringComparer.OrdinalIgnoreCase)
        {
            ["users"] = FullCrudWithAssign(),
            ["equipment"] = FullCrudWithAssign(),
            ["history"] = ViewOnly(),
            ["requests"] = FullCrudWithAssign(),
            ["repairs"] = CrudNoAssign(),
            ["replacements"] = CrudNoAssign(),
            ["maintenance"] = FullCrudWithAssign(),
            ["sanitation"] = CrudNoAssign(),
            ["appeals"] = FullCrudWithAssign(),
            ["notifications"] = ViewOnly(),
            ["reports"] = ViewOnly(),
            ["logs"] = ViewOnly()
        };
    }

    private static SectionPermissions ViewOnly() => new() { CanView = true };

    private static SectionPermissions CrudNoAssign() => new()
    {
        CanView = true,
        CanAdd = true,
        CanEdit = true,
        CanDelete = true
    };

    private static SectionPermissions FullCrudWithAssign() => new()
    {
        CanView = true,
        CanAdd = true,
        CanEdit = true,
        CanAssign = true,
        CanDelete = true
    };
}
