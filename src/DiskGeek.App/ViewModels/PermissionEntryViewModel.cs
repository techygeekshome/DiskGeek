using DiskGeek.Core.Permissions;

namespace DiskGeek.App.ViewModels;

/// <summary>Bindable wrapper around a <see cref="PermissionEntry"/> for the Permissions dialog.</summary>
public sealed class PermissionEntryViewModel
{
    public PermissionEntryViewModel(PermissionEntry model) => Model = model;

    public PermissionEntry Model { get; }

    public string IdentityName => Model.IdentityName;
    public string Rights => Model.IsDeny ? $"Deny: {Model.Rights}" : Model.Rights;
    public string RightsColor => Model.IsDeny ? "#d93025" : "#1a7f37";
    public string InheritedLabel => Model.IsInherited ? "Inherited" : "Explicit";
}
