using System;
using System.Collections.Generic;
using System.Text;

namespace yunyuketanga.Services;

public interface IDebug
{
    void WriteLine(string message);

    void Write(string message);
}
