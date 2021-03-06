﻿namespace OpenVIII.Fields.Scripts.Instructions
{
    internal sealed class THROUGHOFF : JsmInstruction
    {
        #region Constructors

        public THROUGHOFF()
        {
        }

        public THROUGHOFF(int parameter, IStack<IJsmExpression> stack)
            : this()
        {
        }

        #endregion Constructors

        #region Methods

        public override string ToString() => $"{nameof(THROUGHOFF)}()";

        #endregion Methods
    }
}