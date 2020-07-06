using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Model.ThreeAddressCode.Instructions;
using Model.Types;

namespace Backend.Transformations.Assembly
{
    internal class ExceptionInformationBuilder
    {
        private readonly Stack<ProtectedBlockBuilder> protectedBlocks = new Stack<ProtectedBlockBuilder>();
        private readonly IList<ProtectedBlock> result = new List<ProtectedBlock>();

        public IList<ProtectedBlock> Build()
        {
            if (protectedBlocks.Count > 0) throw new Exception("Protected Blocks not generated correctly");
            return result;
        }

        public void BeginProtectedBlockAt(uint offset)
        {
            var protectedBlockBuilder = new ProtectedBlockBuilder {TryStart = offset, HandlerCount = 1};
            protectedBlocks.Push(protectedBlockBuilder);
        }

        public bool CurrentProtectedBlockStartsAt(uint offset) => protectedBlocks.Count > 0 && protectedBlocks.Peek().TryStart.Equals(offset);
        public void IncrementCurrentProtectedBlockExpectedHandlers() => protectedBlocks.Peek().HandlerCount++;

        public void AddHandlerToCurrentProtectedBlock(uint offset, ExceptionHandlerBlockKind handlerBlockKind, IType exceptionType) =>
            protectedBlocks
                .Peek()
                .EndPreviousRegionAt(offset)
                .Handlers.Add(new ProtectedBlockHandlerBuilder
                {
                    HandlerStart = offset,
                    HandlerBlockKind = handlerBlockKind,
                    ExceptionType = exceptionType
                });

        public void AddHandlerToCurrentProtectedBlock(uint offset, ExceptionHandlerBlockKind handlerBlockKind) =>
            AddHandlerToCurrentProtectedBlock(offset, handlerBlockKind, null);

        // filter is a special case since it has a two regions (filter and handler). A filter in this TAC is modeled as two FilterInstruction
        // with different kinds, one for each region. If the previous region is a Filter, it must be ended only if it is in it's handler region.
        public void AddFilterHandlerToCurrentProtectedBlock(uint offset, FilterInstructionKind kind, IType exceptionType)
        {
            var protectedBlockBuilder = protectedBlocks.Peek();

            bool EndPreviousHandlerCondition() => protectedBlockBuilder.Handlers.Last().HandlerBlockKind != ExceptionHandlerBlockKind.Filter ||
                                                  kind == FilterInstructionKind.FilterSection;

            protectedBlockBuilder.EndPreviousRegionAt(offset, EndPreviousHandlerCondition);

            switch (kind)
            {
                case FilterInstructionKind.FilterSection:
                    protectedBlockBuilder.Handlers.Add(new ProtectedBlockHandlerBuilder
                    {
                        FilterStart = offset,
                        HandlerBlockKind = ExceptionHandlerBlockKind.Filter
                    });
                    break;
                case FilterInstructionKind.FilterHandler:
                    var handler = protectedBlockBuilder.Handlers.Last();
                    handler.HandlerStart = offset;
                    handler.ExceptionType = exceptionType;
                    break;
                default: throw kind.ToUnknownValueException();
            }
        }

        public void EndCurrentProtectedBlockIfAppliesAt(uint offset)
        {
            if (protectedBlocks.Count > 0 && protectedBlocks.Peek().AllHandlersAdded())
            {
                EndCurrentProtectedBlockAt(offset);
            }
        }

        public void EndCurrentProtectedBlockAt(uint offset)
        {
            var exceptionBlockBuilder = protectedBlocks.Pop();
            exceptionBlockBuilder
                .Handlers
                .Last()
                .HandlerEnd = offset;
            result.AddRange(exceptionBlockBuilder.Build());
        }


        private class ProtectedBlockBuilder
        {
            private uint? tryStart;

            public uint TryStart
            {
                get => tryStart ?? throw new Exception("TryStart was not set");
                set
                {
                    if (tryStart != null) throw new Exception("TryStart was already set");
                    tryStart = value;
                }
            }

            private uint? tryEnd;

            private uint TryEnd
            {
                get => tryEnd ?? throw new Exception("TryEnd was not set");
                set
                {
                    if (tryEnd != null) throw new Exception("TryEnd was already set");
                    tryEnd = value;
                }
            }

            public uint HandlerCount { get; set; }

            public bool AllHandlersAdded() => HandlerCount == Handlers.Count;

            public readonly IList<ProtectedBlockHandlerBuilder> Handlers = new List<ProtectedBlockHandlerBuilder>();

            public ProtectedBlockBuilder EndPreviousRegionAt(uint offset) => EndPreviousRegionAt(offset, () => true);

            public ProtectedBlockBuilder EndPreviousRegionAt(uint offset, Func<bool> multipleHandlerCondition)
            {
                if (Handlers.Count == 0) // first handler, ends try region
                {
                    TryEnd = offset;
                }
                else if (multipleHandlerCondition()) // multiple handlers. End previous handler conditionally
                {
                    Handlers.Last().HandlerEnd = offset;
                }

                return this;
            }

            // try with multiple handlers is modelled as multiple try instructions with the same label but different handlers.
            public IList<ProtectedBlock> Build() =>
                Handlers
                    .Select(handlerBuilder => handlerBuilder.Build())
                    .Select(handler => new ProtectedBlock(TryStart, TryEnd) {Handler = handler})
                    .ToList();
        }

        private class ProtectedBlockHandlerBuilder
        {
            private uint? filterStart;

            public uint FilterStart
            {
                get => filterStart ?? throw new Exception("FilterStart was not set");
                set
                {
                    if (filterStart != null) throw new Exception("FilterStart was already set");
                    filterStart = value;
                }
            }

            private uint? handlerStart;

            public uint HandlerStart
            {
                get => handlerStart ?? throw new Exception("HandlerStart was not set");
                set
                {
                    if (handlerStart != null) throw new Exception("HandlerStart was already set");
                    handlerStart = value;
                }
            }

            private uint? handlerEnd;

            public uint HandlerEnd
            {
                get => handlerEnd ?? throw new Exception("HandlerEnd was not set");
                set
                {
                    if (handlerEnd != null) throw new Exception("HandlerEnd was already set");
                    handlerEnd = value;
                }
            }

            private ExceptionHandlerBlockKind? handlerBlockKind;

            public ExceptionHandlerBlockKind HandlerBlockKind
            {
                get => handlerBlockKind ?? throw new Exception("HandlerBlockKind was not set");
                set
                {
                    if (handlerBlockKind != null) throw new Exception("HandlerBlockKind was already set");
                    handlerBlockKind = value;
                }
            }

            private IType exceptionType;

            public IType ExceptionType
            {
                get => exceptionType ?? throw new Exception("ExceptionType was not set");
                set
                {
                    if (exceptionType != null) throw new Exception("ExceptionType was already set");
                    exceptionType = value;
                }
            }

            public IExceptionHandler Build()
            {
                switch (HandlerBlockKind)
                {
                    case ExceptionHandlerBlockKind.Filter:
                        return new FilterExceptionHandler(FilterStart, HandlerStart, HandlerEnd, ExceptionType);
                    case ExceptionHandlerBlockKind.Catch:
                        return new CatchExceptionHandler(HandlerStart, HandlerEnd, ExceptionType);
                    case ExceptionHandlerBlockKind.Fault:
                        return new FaultExceptionHandler(HandlerStart, HandlerEnd);
                    case ExceptionHandlerBlockKind.Finally:
                        return new FinallyExceptionHandler(HandlerStart, HandlerEnd);
                    default: throw new UnknownValueException<ExceptionHandlerBlockKind>(HandlerBlockKind);
                }
            }
        }
    }
}