using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public abstract partial class OwnerWallet : ObservableObject, INotifyDataErrorInfo
  {
    protected Dictionary<string, string> PropertiesParameter = new()
    {
      ["address"] = nameof(AddressWithdrawal),
      ["fee"] = nameof(FeeWithdrawal)
    };

    Dictionary<string, string> ErrorsProperty = [];

    public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

    [ObservableProperty]
    public partial string ErrorWithdrawal { get; set; }

    [ObservableProperty]
    public partial string AddressWithdrawal { get; set; }

    [ObservableProperty]
    public partial decimal? FeeWithdrawal { get; set; }


    public bool HasErrors
    {
      get
      {
        return ErrorsProperty.Count > 0;
      }
    }

    public IEnumerable GetErrors(string propertyName)
    {
      if (propertyName != null && ErrorsProperty.TryGetValue(propertyName, out string error))
        return new[] { error };

      return Array.Empty<string>();
    }

    internal bool MarkErrors(Exception exception)
    {
      foreach (string parameter in (exception as RPCException)?.Parameters ?? [])
        if (PropertiesParameter.TryGetValue(parameter, out string property))
        {
          ErrorsProperty[property] = exception.Message;
          ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
        }

      return ErrorsProperty.Count > 0;
    }

    internal void ClearErrors()
    {
      ErrorWithdrawal = null;

      List<string> properties = [.. ErrorsProperty.Keys];
      ErrorsProperty.Clear();

      properties.ForEach(p => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(p)));
    }
  }
}
