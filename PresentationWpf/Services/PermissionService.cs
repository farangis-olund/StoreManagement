using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Infrastructure.Security;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationWpf.Services;

public class PermissionService : ObservableObject, IPermissionChecker
{
    private HashSet<string> _currentPermissions = new();

    public void SetPermissions(IEnumerable<string> permissions)
    {
        _currentPermissions = new HashSet<string>(permissions);
        OnPropertyChanged(nameof(CurrentPermissions));
    }

    public bool Has(string key) => _currentPermissions.Contains(key);

    public IEnumerable<string> CurrentPermissions => _currentPermissions;
}
