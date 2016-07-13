using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace VoxelWorldEngine.Util
{
    public class SingleAccessVerify
    {
        readonly List<Token> tokens = new List<Token>();

        private void Increment(Token token)
        {
            lock (tokens)
            {
                if (tokens.Count > 0)
                    throw new InvalidOperationException();
                tokens.Add(token);
            }
        }

        private void Decrement(Token token)
        {
            lock (tokens)
            {
                tokens.Remove(token);
            }
        }

        public Token Acquire([CallerFilePath] string path = null, [CallerLineNumber] int line = 0)
        {
            return new Token(this, path, line);
        }

        public class Token : IDisposable
        {
            readonly SingleAccessVerify parent;
            private readonly int line;
            private readonly string path;

            public Token(SingleAccessVerify parent, string path, int line)
            {
                this.parent = parent;
                this.path = path;
                this.line = line;
                parent.Increment(this);
            }

            public void Dispose()
            {
                parent.Decrement(this);
            }

            public override string ToString()
            {
                return $"{path}:{line}";
            }
        }
    }
}