﻿using System;
using FF8.Framework;

namespace FF8.JSM.Instructions
{
    internal sealed class Unknown15 : JsmInstruction
    {
        private IJsmExpression _arg0;

        public Unknown15(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public Unknown15(Int32 parameter, IStack<IJsmExpression> stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(Unknown15)}({nameof(_arg0)}: {_arg0})";
        }
    }
}