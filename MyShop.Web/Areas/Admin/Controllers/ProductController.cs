using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyShop.DataAccess.Data;
using MyShop.Entities.Models;
using MyShop.Entities.Repositories;
using MyShop.Entities.ViewModels;
using MyShop.Utilities;

namespace MyShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.AdminRole)]
    public class ProductController : Controller
    {
        private readonly IUnitOfWork unitOfWork;
        // we use this to get the path of the wwwroot folder
        // so we can save the image in it
        private readonly IWebHostEnvironment webHostEnvironment;
        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            this.unitOfWork = unitOfWork;
            this.webHostEnvironment = webHostEnvironment;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult GetData()
        {
            var products = unitOfWork.Product.GetAll(IncludeWord: "Category");
            return Json(new { data = products });
        }
        [HttpGet]
        public IActionResult Create()
        {
            ProductVM productVM = new ProductVM()
            {
                Product = new Product(),
                CategoryList = unitOfWork.Category.GetAll().Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                })
            };
            return View(productVM);
        }

        [HttpPost]
        // use it with any post request to prevent CSRF attacks
        [ValidateAntiForgeryToken] // it is used to prevent CSRF(Cross Side Forgery Attacks) attacks
        // enctype="multipart/form-data" we add this to the form in the view to make him know that we will upload files
        // this is the name insid the input tag in the view
        public IActionResult Create(ProductVM productVM, IFormFile file)
        {
            if (ModelState.IsValid)
            {
                string rootPath = webHostEnvironment.WebRootPath;
                if (file != null)
                {
                    string fileName = Guid.NewGuid().ToString();
                    var uploads = Path.Combine(rootPath, @"Images\Products");
                    var extension = Path.GetExtension(file.FileName);
                    using (var fileStream = new FileStream(Path.Combine(uploads, fileName + extension), FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }
                    productVM.Product.ImageUrl = @"\Images\Products\" + fileName + extension;
                }
                unitOfWork.Product.Add(productVM.Product);
                unitOfWork.Complete();
                TempData["Create"] = "Product created successfully";
                return RedirectToAction("Index");
            }
            return View(productVM);
        }

        [HttpGet]
        public IActionResult Edit(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            ProductVM productVM = new ProductVM()
            {
                Product = unitOfWork.Product.GetFirstOrDefault(u => u.Id == id),
                CategoryList = unitOfWork.Category.GetAll().Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                })
            };
            return View(productVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(ProductVM productVM, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                string rootPath = webHostEnvironment.WebRootPath;
                if (file != null)
                {
                    string fileName = Guid.NewGuid().ToString();
                    var uploads = Path.Combine(rootPath, @"Images\Products");
                    var extension = Path.GetExtension(file.FileName);
                    // to delete the old image form the server after update
                    if(productVM.Product.ImageUrl != null)
                    {
                        var oldImagePath = Path.Combine(rootPath, productVM.Product.ImageUrl.TrimStart('\\'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }
                    using (var fileStream = new FileStream(Path.Combine(uploads, fileName + extension), FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }
                    productVM.Product.ImageUrl = @"\Images\Products\" + fileName + extension;
                }
                unitOfWork.Product.Update(productVM.Product);
                unitOfWork.Complete();
                TempData["Update"] = "Product updated successfully";
                return RedirectToAction("Index");
            }
            return View(productVM);
        }


        [HttpDelete]
        public IActionResult DeleteProduct(int? id)
        {
            var product = unitOfWork.Product.GetFirstOrDefault(u => u.Id == id);
            if (product == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }
            unitOfWork.Product.Remove(product);
            var oldImagePath = Path.Combine(webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('\\'));
            if (System.IO.File.Exists(oldImagePath))
            {
                System.IO.File.Delete(oldImagePath);
            }
            unitOfWork.Complete();
            //TempData["Delete"] = "Product deleted successfully";
            return Json(new { success = true, message = "Delete successful" });
        }
    }
}
