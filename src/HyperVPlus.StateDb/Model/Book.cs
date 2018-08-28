using System;
using System.Collections.Generic;

namespace HyperVPlus.StateDb.Model
{
    public class Operation
    {
        public Guid Id { get; set; }
        public virtual List<OperationLog> LogEntries { get; set; }
    }

    public class Machine
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
   
    }

    public class OperationLog
    {
        public Guid Id { get; set; }
        
        public string Message { get; set; }
        public Operation Operation { get; set; }
    }


}