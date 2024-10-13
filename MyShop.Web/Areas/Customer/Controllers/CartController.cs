using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyShop.Entities.Models;
using MyShop.Entities.Repositories;
using MyShop.Entities.ViewModels;
using MyShop.Utilities;
using Stripe.Checkout;
using System.Security.Claims;

namespace MyShop.Web.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork unitOfWork;
        public ShoppingCartVM ShoppingCartVM { get; set; }
        public int TotalCarts { get; set; }
        public CartController(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            var ShoppingCartVM = new ShoppingCartVM()
            {
                CartsList = unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value, IncludeWord: "Product")
            };
            foreach (var cart in ShoppingCartVM.CartsList)
			{
				ShoppingCartVM.TotalCarts += (cart.Count * cart.Product.Price);
			}
            return View(ShoppingCartVM);
        }

        public IActionResult Plus(int cartId)
		{
			var cart = unitOfWork.ShoppingCart.GetFirstOrDefault(u => u.Id == cartId);
			unitOfWork.ShoppingCart.IncreaseCount(cart, 1);
			unitOfWork.Complete();
			return RedirectToAction("Index");
		}

        public IActionResult Minus(int cartId)
        {
            var cart = unitOfWork.ShoppingCart.GetFirstOrDefault(u => u.Id == cartId);
            if(cart.Count == 1)
			{
				unitOfWork.ShoppingCart.Remove(cart);
                var count = unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count() - 1;
                HttpContext.Session.SetInt32(SD.SessionKey, count);
			}
			else
			{
				unitOfWork.ShoppingCart.DecreaseCount(cart, 1);
			}
            unitOfWork.Complete();
            return RedirectToAction("Index");
        }

        public IActionResult Remove(int cartId)
        {
            var cart = unitOfWork.ShoppingCart.GetFirstOrDefault(u => u.Id == cartId);
            unitOfWork.ShoppingCart.Remove(cart);
            unitOfWork.Complete();
            var count = unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count();
            HttpContext.Session.SetInt32(SD.SessionKey, count);
            return RedirectToAction("Index");
        }
        [HttpGet]
		public IActionResult Summary()
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
			var ShoppingCartVM = new ShoppingCartVM()
			{
				CartsList = unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value, IncludeWord: "Product"),
				OrderHeader = new()
			};
            ShoppingCartVM.OrderHeader.ApplicationUser = unitOfWork.ApplicationUser.GetFirstOrDefault(u => u.Id == claim.Value);

            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
			ShoppingCartVM.OrderHeader.Address = ShoppingCartVM.OrderHeader.ApplicationUser.Address;
			ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
			foreach (var cart in ShoppingCartVM.CartsList)
			{
				ShoppingCartVM.OrderHeader.TotalPrice += (cart.Count * cart.Product.Price);
			}
			return View(ShoppingCartVM);
		}

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Summary")]
        public IActionResult POSTSummary(ShoppingCartVM shoppingCartVM)
        {
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            shoppingCartVM.CartsList = unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value, IncludeWord: "Product");

            shoppingCartVM.OrderHeader.OrderStatus = SD.Pending;
            shoppingCartVM.OrderHeader.PaymentStatus = SD.Pending;
            shoppingCartVM.OrderHeader.OrderDate = DateTime.Now;
            shoppingCartVM.OrderHeader.ApplicationUserId = claim.Value;
            
            foreach (var cart in shoppingCartVM.CartsList)
			{
				shoppingCartVM.OrderHeader.TotalPrice += (cart.Count * cart.Product.Price);
			}
            unitOfWork.OrderHeader.Add(shoppingCartVM.OrderHeader);
            unitOfWork.Complete();

            foreach (var cart in shoppingCartVM.CartsList)
            {
                OrderDetails orderDetails = new OrderDetails
                {
                    ProductId = cart.ProductId,
                    OrderHeaderId = shoppingCartVM.OrderHeader.Id,
                    Price = cart.Product.Price,
                    Count = cart.Count
                };
                unitOfWork.OrderDetails.Add(orderDetails);
                unitOfWork.Complete();
            }

            // now work with stripe
            var domain = "https://localhost:44379/";
            var options = new SessionCreateOptions
			{
				LineItems = new List<SessionLineItemOptions>(),

                Mode = "payment",
                SuccessUrl = domain+$"customer/cart/orderConfirmation?id={shoppingCartVM.OrderHeader.Id}",
                CancelUrl = domain+$"customer/cart/index",
			};

            foreach (var item in shoppingCartVM.CartsList)
            {
                var sessionlineoptions = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
						UnitAmount = (long)(item.Product.Price * 100),
						Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Name
                        },
                    },
                    Quantity = item.Count
                };
                options.LineItems.Add(sessionlineoptions);
            }

			var service = new SessionService();
			Session session = service.Create(options);
			shoppingCartVM.OrderHeader.SessionId = session.Id;
            unitOfWork.OrderHeader.Update(shoppingCartVM.OrderHeader);
            unitOfWork.Complete();

            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
		}

        public IActionResult OrderConfirmation(int id)
		{
			OrderHeader orderHeader = unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == id);
            var service = new SessionService();
			Session session = service.Get(orderHeader.SessionId);

            if(session.PaymentStatus.ToLower() == "paid")
            {
                unitOfWork.OrderHeader.UpdateOrderStatus(id, SD.Approve, SD.Approve);
                orderHeader.PaymentIntentId = session.PaymentIntentId;
                unitOfWork.Complete();
			}
            List<ShoppingCart> shoppingCarts = unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
            unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            unitOfWork.Complete();
            return View(id);
		}
	}
}
