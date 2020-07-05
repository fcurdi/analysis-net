using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Model.Types;

namespace Backend.Transformations.Assembly
{
    internal class ProtectedBlockBuilder
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

        public ProtectedBlockBuilder EndPreviousRegionWith(uint offset) => EndPreviousRegionWith(offset, () => true);

        public ProtectedBlockBuilder EndPreviousRegionWith(uint offset, Func<bool> multipleHandlerCondition)
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

        public IList<ProtectedBlock> Build() =>
            Handlers
                .Select(handlerBuilder => handlerBuilder.Build())
                .Select(handler => new ProtectedBlock(TryStart, TryEnd) {Handler = handler})
                .ToList();
    }

    internal class ProtectedBlockHandlerBuilder
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