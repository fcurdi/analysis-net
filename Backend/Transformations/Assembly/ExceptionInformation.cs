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

        public void BeginProtectedBlockAt(string label)
        {
            var protectedBlockBuilder = new ProtectedBlockBuilder {TryStart = label, HandlerCount = 1};
            protectedBlocks.Push(protectedBlockBuilder);
        }

        public void IncrementCurrentProtectedBlockExpectedHandlers() => protectedBlocks.Peek().HandlerCount++;

        public void AddHandlerToCurrentProtectedBlock(string label, ExceptionHandlerBlockKind handlerBlockKind, IType exceptionType) =>
            protectedBlocks
                .Peek()
                .EndPreviousRegionAt(label)
                .Handlers.Add(new ProtectedBlockHandlerBuilder
                {
                    HandlerStart = label,
                    HandlerBlockKind = handlerBlockKind,
                    ExceptionType = exceptionType
                });

        public void AddHandlerToCurrentProtectedBlock(string label, ExceptionHandlerBlockKind handlerBlockKind) =>
            AddHandlerToCurrentProtectedBlock(label, handlerBlockKind, null);

        // filter is a special case since it has a two regions (filter and handler). A filter in this TAC is modeled as two FilterInstruction
        // with different kinds, one for each region. If the previous region is a Filter, it must be ended only if it is in it's handler region.
        public void AddFilterHandlerToCurrentProtectedBlock(string label, FilterInstructionKind kind, IType exceptionType)
        {
            var protectedBlockBuilder = protectedBlocks.Peek();

            bool EndPreviousHandlerCondition() => protectedBlockBuilder.Handlers.Last().HandlerBlockKind != ExceptionHandlerBlockKind.Filter ||
                                                  kind == FilterInstructionKind.FilterSection;

            protectedBlockBuilder.EndPreviousRegionAt(label, EndPreviousHandlerCondition);

            switch (kind)
            {
                case FilterInstructionKind.FilterSection:
                    protectedBlockBuilder.Handlers.Add(new ProtectedBlockHandlerBuilder
                    {
                        FilterStart = label,
                        HandlerBlockKind = ExceptionHandlerBlockKind.Filter
                    });
                    break;
                case FilterInstructionKind.FilterHandler:
                    var handler = protectedBlockBuilder.Handlers.Last();
                    handler.HandlerStart = label;
                    handler.ExceptionType = exceptionType;
                    break;
                default: throw kind.ToUnknownValueException();
            }
        }

        public void EndCurrentProtectedBlockIfAppliesAt(string label)
        {
            if (protectedBlocks.Count > 0 && protectedBlocks.Peek().AllHandlersAdded())
            {
                EndCurrentProtectedBlockAt(label);
            }
        }

        public void EndCurrentProtectedBlockAt(string label)
        {
            var exceptionBlockBuilder = protectedBlocks.Pop();
            exceptionBlockBuilder
                .Handlers
                .Last()
                .HandlerEnd = label;
            result.AddRange(exceptionBlockBuilder.Build());
        }
        
        private class ProtectedBlockBuilder
        {
            private string tryStart;

            public string TryStart
            {
                get => tryStart ?? throw new Exception("TryStart was not set");
                set
                {
                    if (tryStart != null) throw new Exception("TryStart was already set");
                    tryStart = value;
                }
            }

            private string tryEnd;

            private string TryEnd
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

            public ProtectedBlockBuilder EndPreviousRegionAt(string label) => EndPreviousRegionAt(label, () => true);

            public ProtectedBlockBuilder EndPreviousRegionAt(string label, Func<bool> multipleHandlerCondition)
            {
                if (Handlers.Count == 0) // first handler, ends try region
                {
                    TryEnd = label;
                }
                else if (multipleHandlerCondition()) // multiple handlers. End previous handler conditionally
                {
                    Handlers.Last().HandlerEnd = label;
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
            private string filterStart;

            public string FilterStart
            {
                get => filterStart ?? throw new Exception("FilterStart was not set");
                set
                {
                    if (filterStart != null) throw new Exception("FilterStart was already set");
                    filterStart = value;
                }
            }

            private string handlerStart;

            public string HandlerStart
            {
                get => handlerStart ?? throw new Exception("HandlerStart was not set");
                set
                {
                    if (handlerStart != null) throw new Exception("HandlerStart was already set");
                    handlerStart = value;
                }
            }

            private string handlerEnd;

            public string HandlerEnd
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