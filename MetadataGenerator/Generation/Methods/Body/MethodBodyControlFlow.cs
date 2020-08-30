using System.Collections.Generic;
using Model;
using ECMA335 = System.Reflection.Metadata.Ecma335;

namespace MetadataGenerator.Generation.Methods.Body
{
    // Control flow is tracked by using SRM.LabelHandle. This allows us to reference instructions in the method body that are targets of
    // branch instructions or part of exception regions. This LabelHandles point a specific part of the InstructionEncoder which translates
    // to the encoded instruction. Since we can target an instruction that is further away in the body, not encoded yet, LabelHandles are defined 
    // with a placeholder target, and then marked when we actually process the target instruction.
    internal class MethodBodyControlFlow
    {
        private readonly ECMA335.InstructionEncoder instructionEncoder;
        private readonly MetadataResolver metadataResolver;
        private readonly IDictionary<string, ECMA335.LabelHandle> labelHandles;

        public MethodBodyControlFlow(ECMA335.InstructionEncoder instructionEncoder, MetadataResolver metadataResolver)
        {
            this.instructionEncoder = instructionEncoder;
            this.metadataResolver = metadataResolver;
            labelHandles = new Dictionary<string, ECMA335.LabelHandle>();
        }

        // get LabelHandle associated with label or define a new one.
        public ECMA335.LabelHandle LabelHandleFor(string label)
        {
            label = label.ToLower();
            if (!labelHandles.TryGetValue(label, out var labelHandle))
            {
                labelHandle = instructionEncoder.DefineLabel();
                labelHandles.Add(label, labelHandle);
            }

            return labelHandle;
        }

        // Defines needed LabelHandles for branch instructions
        public void ProcessBranchTargets(IList<string> targets)
        {
            foreach (var target in targets)
            {
                LabelHandleFor(target);
            }
        }

        // if there is an associated LabelHandle then mark it with its real position in the InstructionEncoder
        public void MarkLabelIfNeeded(string label)
        {
            if (labelHandles.TryGetValue(label.ToLower(), out var labelHandle))
            {
                instructionEncoder.MarkLabel(labelHandle);
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
                    {
                        var handler = (FilterExceptionHandler) protectedBlock.Handler;
                        var filterStart = LabelHandleFor(handler.FilterStart);
                        controlFlowBuilder.AddFilterRegion(tryStart, tryEnd, handlerStart, handlerEnd, filterStart);
                        break;
                    }
                    case ExceptionHandlerBlockKind.Catch:
                    {
                        var handler = (CatchExceptionHandler) protectedBlock.Handler;
                        controlFlowBuilder.AddCatchRegion(
                            tryStart,
                            tryEnd,
                            handlerStart,
                            handlerEnd,
                            metadataResolver.HandleOf(handler.ExceptionType));
                        break;
                    }
                    case ExceptionHandlerBlockKind.Fault:
                        controlFlowBuilder.AddFaultRegion(tryStart, tryEnd, handlerStart, handlerEnd);
                        break;
                    case ExceptionHandlerBlockKind.Finally:
                        controlFlowBuilder.AddFinallyRegion(tryStart, tryEnd, handlerStart, handlerEnd);
                        break;
                    default: throw protectedBlock.Handler.Kind.ToUnknownValueException();
                }
            }
        }
    }
}