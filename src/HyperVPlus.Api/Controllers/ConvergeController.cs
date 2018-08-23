using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.StateDb;
using HyperVPlus.StateDb.Model;
using HyperVPlus.StateDb.MySql;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Rebus.Bus;

namespace HyperVPlus.Api.Controllers
{
    [Route("api/[controller]")]
    public class ConvergeController : Controller
    {
        private readonly IBus _bus;
        private readonly StateStoreContext _dbContext;

        public ConvergeController(IBus bus, StateStoreContext dbContext)
        {
            _bus = bus;
            _dbContext = dbContext;
        }

        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] {"value1", "value2"};
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]        
        public async Task<Operation> Post([FromBody] Config config)
        {

            //var config = JsonConvert.DeserializeObject<Config>(value);

            var operation = new Operation{Id = Guid.NewGuid()};
            _dbContext.Add(operation);
            await _dbContext.SaveChangesAsync().ConfigureAwait(false);


            await _bus.Send(new InitiateVirtualMachineConvergeCommand {Config = config.Configurations[0], ConvergeProcessId = operation.Id })
                .ConfigureAwait(false);

            return operation;
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }



}
