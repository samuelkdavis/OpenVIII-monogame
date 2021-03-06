﻿namespace OpenVIII.Fields.Scripts.Instructions
{
    /// <summary>
    /// Animation Synchronize. Pauses this script until the entity's current animation is finished playing.
    /// </summary>
    /// <see cref="http://wiki.ffrtt.ru/index.php?title=FF8/Field/Script/Opcodes/044_ANIMESYNC"/>
    public sealed class ANIMESYNC : JsmInstruction
    {
        #region Constructors

        public ANIMESYNC()
        {
        }

        public ANIMESYNC(int parameter, IStack<IJsmExpression> stack)
            : this()
        {
        }

        #endregion Constructors

        #region Methods

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services) => sw.Format(formatterContext, services)
                .Await()
                .Property(nameof(FieldObject.Animation))
                .Method(nameof(FieldObjectAnimation.Wait))
                .Comment(nameof(ANIMESYNC));

        public override IAwaitable TestExecute(IServices services) => ServiceId.Field[services].Engine.CurrentObject.Animation.Wait();

        public override string ToString() => $"{nameof(ANIMESYNC)}()";

        #endregion Methods
    }
}