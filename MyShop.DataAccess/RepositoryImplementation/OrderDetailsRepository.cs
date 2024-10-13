using MyShop.DataAccess.Data;
using MyShop.Entities.Models;
using MyShop.Entities.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyShop.DataAccess.RepositoryImplementation
{
    public class OrderDetailsRepository : GenericRepository<OrderDetails>, IOrderDetailsRepository
	{
		private readonly ApplicationDbContext context;
        public OrderDetailsRepository(ApplicationDbContext context) : base(context)
        {
			this.context = context;
        }

		public void Update(OrderDetails orderDetails)
		{
			context.OrderDetails.Update(orderDetails);
		}
	}
}
