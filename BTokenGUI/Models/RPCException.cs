using System;
using System.Collections.Generic;

namespace BTokenGUI.Models;

public class RPCException : InvalidOperationException
{
  public readonly List<string> Parameters;


  public RPCException(string message, List<string> parameters)
    : base(message)
  {
    Parameters = parameters;
  }
}
