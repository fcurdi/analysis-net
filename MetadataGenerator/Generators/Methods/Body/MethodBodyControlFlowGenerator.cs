using System;
using System.Collections.Generic;
using MetadataGenerator.Metadata;
using Model;
using ECMA335 = System.Reflection.Metadata.Ecma335;

namespace MetadataGenerator.Generators.Methods.Body
{
    internal class MethodBodyControlFlowGenerator
    {
        private readonly IDictionary<string, ECMA335.LabelHandle> labelHandles = new Dictionary<string, ECMA335.LabelHandle>();
        private readonly ECMA335.InstructionEncoder instructionEncoder;
        private readonly MetadataContainer metadataContainer;

        public MethodBodyControlFlowGenerator(ECMA335.InstructionEncoder instructionEncoder, MetadataContainer metadataContainer)
        {
            this.instructionEncoder = instructionEncoder;
            this.metadataContainer = metadataContainer;
        }

        public ECMA335.LabelHandle LabelHandleFor(string label)
        {
            label = label.ToLower();
            if (!labelHandles.TryGetValue(label, out var labelHandle))
            {
                labelHandle = instructionEncoder.DefineLabel();
                labelHandles.Add(label, labelHandle);


                // FIXME remove
                unmarkedLabels.Add(labelHandle);
                //
            }

            return labelHandle;
        }


        public void MarkCurrentLabel()
        {
            if (labelHandles.TryGetValue(instructionEncoder.CurrentLabelString(), out var labelHandle))
            {
                instructionEncoder.MarkLabel(labelHandle);

                //FIXME remove
                unmarkedLabels.Remove(labelHandle);
                //
            }
        }

        public void ProcessExceptionInformation(IList<ProtectedBlock> exceptionInformation)
        {
            var controlFlowBuilder = instructionEncoder.ControlFlowBuilder;
            foreach (var protectedBlock in exceptionInformation)
            {
                var tryStart = LabelHandleFor(protectedBlock.Start);
                var tryEnd = LabelHandleFor(protectedBlock.End);
                var handlerStart = LabelHandleFor(protectedBlock.Handler.Start);
                var handlerEnd = LabelHandleFor(protectedBlock.Handler.End);

                switch (protectedBlock.Handler.Kind)
                {
                    case ExceptionHandlerBlockKind.Filter:
                        var filterStart = LabelHandleFor(((FilterExceptionHandler) protectedBlock.Handler).FilterStart);
                        controlFlowBuilder.AddFilterRegion(tryStart, tryEnd, handlerStart, handlerEnd, filterStart);
                        break;
                    case ExceptionHandlerBlockKind.Catch:
                        var catchType = ((CatchExceptionHandler) protectedBlock.Handler).ExceptionType;
                        controlFlowBuilder.AddCatchRegion(tryStart, tryEnd, handlerStart, handlerEnd,
                            metadataContainer.ResolveReferenceHandleFor(catchType));
                        break;
                    case ExceptionHandlerBlockKind.Fault:
                    case ExceptionHandlerBlockKind.Finally:
                        controlFlowBuilder.AddFinallyRegion(tryStart, tryEnd, handlerStart, handlerEnd);
                        break;
                }
            }
        }

        // FIXME only to develop while all instructions are not generated correctly because if some label results unmarked then 
        // SRM throws an exception. 
        private readonly IList<ECMA335.LabelHandle> unmarkedLabels = new List<ECMA335.LabelHandle>();

        [Obsolete("Use only to develop while there are still instructions not being generated correctly")]
        public void MarkAllUnmarkedLabels()
        {
            foreach (var label in unmarkedLabels)
            {
                instructionEncoder.MarkLabel(label);
            }
        }
    }
}