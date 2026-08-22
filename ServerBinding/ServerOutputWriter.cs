// Copyright 2026 ZiYueCommentary
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Text;

namespace CbmrWebAdmin.ServerBinding;

internal sealed class ServerOutputWriter(TextWriter originalWriter) : TextWriter
{
    private const int MaximumLineCount = 100;
    private readonly Queue<string> _lines = new Queue<string>();
    private readonly StringBuilder _currentLine = new StringBuilder();
    private readonly Lock _sync = new Lock();

    public TextWriter OriginalWriter => originalWriter;

    public override Encoding Encoding => originalWriter.Encoding;

    public IReadOnlyList<string> GetLines()
    {
        lock (_sync)
        {
            List<string> lines = [.. _lines];
            if (_currentLine.Length > 0)
            {
                lines.Add(_currentLine.ToString());
            }

            return lines;
        }
    }

    public override void Write(char value)
    {
        lock (_sync)
        {
            originalWriter.Write(value);
            Append(value);
        }
    }

    public override void Write(string? value)
    {
        if (value is null) return;

        lock (_sync)
        {
            originalWriter.Write(value);
            foreach (char character in value)
            {
                Append(character);
            }
        }
    }

    public override void Flush()
    {
        lock (_sync)
        {
            originalWriter.Flush();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Flush();
        }

        base.Dispose(disposing);
    }

    private void Append(char value)
    {
        if (value == '\n')
        {
            AddLine(_currentLine.ToString());
            _currentLine.Clear();
            return;
        }

        if (value != '\r')
        {
            _currentLine.Append(value);
        }
    }

    private void AddLine(string line)
    {
        _lines.Enqueue(line);
        while (_lines.Count > MaximumLineCount)
        {
            _lines.Dequeue();
        }
    }
}