﻿using CheckDrive.ApiContracts;
using CheckDrive.ApiContracts.MechanicHandover;
using CheckDrive.Web.Stores.Cars;
using CheckDrive.Web.Stores.DoctorReviews;
using CheckDrive.Web.Stores.Drivers;
using CheckDrive.Web.Stores.MechanicHandovers;
using CheckDrive.Web.Stores.Mechanics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CheckDrive.Web.Controllers
{
    public class MechanicHandoversController : Controller
    {
        private readonly IMechanicHandoverDataStore _mechanicHandoverDataStore;
        private readonly IDriverDataStore _driverDataStore;
        private readonly ICarDataStore _carDataStore;
        private readonly IMechanicDataStore _mechanicDataStore;
        private readonly IDoctorReviewDataStore _doctorReviewDataStore;

        public MechanicHandoversController(IMechanicHandoverDataStore mechanicHandoverDataStore, IDriverDataStore driverDataStore, ICarDataStore carDataStore, IMechanicDataStore mechanicDataStore, IDoctorReviewDataStore doctorReviewDataStore)
        {
            _mechanicHandoverDataStore = mechanicHandoverDataStore;
            _driverDataStore = driverDataStore;
            _carDataStore = carDataStore;
            _mechanicDataStore = mechanicDataStore;
            _doctorReviewDataStore = doctorReviewDataStore;
        }

        public async Task<IActionResult> Index(int? pageNumber, string? searchString, DateTime? date)
        {
            var response = await _mechanicHandoverDataStore.GetMechanicHandoversAsync(pageNumber, searchString, date, null, 1);

            ViewBag.PageSize = response.PageSize;
            ViewBag.PageCount = response.TotalPages;
            ViewBag.TotalCount = response.TotalCount;
            ViewBag.CurrentPage = response.PageNumber;
            ViewBag.HasPreviousPage = response.HasPreviousPage;
            ViewBag.HasNextPage = response.HasNextPage;

            var mechanicHandovers = response.Data.Select(r => new
            {
                r.Id,
                IsHanded = (bool)r.IsHanded ? "Topshirildi" : "Topshirilmadi",
                r.Comments,
                Status = ((StatusForDto)r.Status) switch
                {
                    StatusForDto.Pending => "Kutilmoqda",
                    StatusForDto.Completed => "Yakunlangan",
                    StatusForDto.Rejected => "Rad etilgan",
                    StatusForDto.Unassigned => "Tayinlanmagan",
                    StatusForDto.RejectedByDriver => "Haydovchi tomonidan rad etilgan",
                    _ => "No`malum holat"
                },
                r.Date,
                r.Distance,
                r.DriverName,
                r.MechanicName,
                r.CarName,
                r.CarId
            }).ToList();

            ViewBag.MechanicHandovers = mechanicHandovers;

            return View();
        }

        public async Task<IActionResult> PersonalIndex(string? searchString, int? pageNumber)
        {
            var response = await _mechanicHandoverDataStore.GetMechanicHandoversAsync(pageNumber, searchString, null, null, 6);

            ViewBag.PageSize = response.PageSize;
            ViewBag.PageCount = response.TotalPages;
            ViewBag.TotalCount = response.TotalCount;
            ViewBag.CurrentPage = response.PageNumber;
            ViewBag.HasPreviousPage = response.HasPreviousPage;
            ViewBag.HasNextPage = response.HasNextPage;

            return View(response.Data);
        }

        public async Task<IActionResult> Create(int? driverId)
        {
            var drivers = await GETDrivers();
            var cars = await GETCars();

            var doctorReviews = await _doctorReviewDataStore.GetDoctorReviewsAsync(null, null, DateTime.Today, true, 1);
            var mechanicHandovers = await _mechanicHandoverDataStore.GetMechanicHandoversAsync(null, null, DateTime.Today, null, 1);

            var accountIdStr = TempData["AccountId"] as string;
            TempData.Keep("AccountId");

            if (int.TryParse(accountIdStr, out int accountId))
            {
                var mechanicResponse = await _mechanicDataStore.GetMechanics(accountId);
                var mechanic = mechanicResponse.Data.First();
                if (mechanic != null)
                {
                    var healthyDrivers = doctorReviews.Data
                        .Select(dr => dr.DriverId)
                        .ToList();

                    var handedDrivers = mechanicHandovers.Data
                        .Select(ma => ma.DriverId)
                        .ToList();

                    var filteredDrivers = drivers
                        .Where(d => healthyDrivers.Contains(int.Parse(d.Value)) && !handedDrivers.Contains(int.Parse(d.Value)))
                        .ToList();

                    var usedCarIds = mechanicHandovers.Data
                        .Select(mh => mh.CarId)
                        .ToList();

                    var filteredCars = cars
                        .Where(c => !usedCarIds.Contains(int.Parse(c.Value)))
                        .ToList();
                    ViewBag.Drivers = new SelectList(filteredDrivers, "Value", "Text", driverId);
                    ViewBag.Cars = new SelectList(filteredCars, "Value", "Text");

                    var selectedDriverName = filteredDrivers.FirstOrDefault(d => d.Value == driverId.ToString())?.Text;
                    ViewBag.SelectedDriverName = selectedDriverName ?? string.Empty;
                    ViewBag.SelectedDriverId = driverId;

                    return View(new MechanicHandoverForCreateDto { DriverId = driverId ?? 0, MechanicId = mechanic.Id });
                }
            }

            return NotFound("Механик не найден для указанного аккаунта.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IsHanded,Comments,MechanicId,Distance,CarId,DriverId")] MechanicHandoverForCreateDto mechanicHandoverForCreateDto)
        {
            if (ModelState.IsValid)
            {
                // Fetch the selected car's details
                var car = await _carDataStore.GetCarAsync(mechanicHandoverForCreateDto.CarId);
                if (car != null && mechanicHandoverForCreateDto.Distance < car.Mileage)
                {
                    ModelState.AddModelError("Distance", "Masofa mashinaning mavjud yurgan masofasidan kam bo'lishi mumkin emas!");
                }

                if (ModelState.IsValid)
                {
                    if (mechanicHandoverForCreateDto.IsHanded == false)
                    {
                        mechanicHandoverForCreateDto.Status = StatusForDto.Rejected;
                    }

                    mechanicHandoverForCreateDto.Date = DateTime.Now;
                    mechanicHandoverForCreateDto.Status = mechanicHandoverForCreateDto.IsHanded ? StatusForDto.Pending : StatusForDto.Rejected;
                    await _mechanicHandoverDataStore.CreateMechanicHandoverAsync(mechanicHandoverForCreateDto);
                    return RedirectToAction(nameof(PersonalIndex));
                }
            }

            var drivers = await GETDrivers();
            var cars = await GETCars();
            ViewBag.Drivers = new SelectList(drivers, "Value", "Text", mechanicHandoverForCreateDto.DriverId);
            ViewBag.Cars = new SelectList(cars, "Value", "Text");

            var selectedDriverName = drivers.FirstOrDefault(d => d.Value == mechanicHandoverForCreateDto.DriverId.ToString())?.Text;
            ViewBag.SelectedDriverName = selectedDriverName ?? string.Empty;
            ViewBag.SelectedDriverId = mechanicHandoverForCreateDto.DriverId;

            return View(mechanicHandoverForCreateDto);
        }


        public async Task<IActionResult> Edit(int id)
        {
            var review = await _mechanicHandoverDataStore.GetMechanicHandoverAsync(id);

            if (review == null)
            {
                return NotFound();
            }

            var drivers = await _driverDataStore.GetDriversAsync(1);
            var cars = await _carDataStore.GetCarsAsync(1);

            ViewBag.DriverSelectList = new SelectList(drivers.Data.Select(driver => new
            {
                Id = driver.Id,
                DisplayText = $"{driver.FirstName} {driver.LastName}"
            }), "Id", "DisplayText");

            ViewBag.CarSelectList = new SelectList(cars.Data.Select(car => new
            {
                Id = car.Id,
                DisplayText = $"{car.Model} ({car.Number})"
            }), "Id", "DisplayText");

            ViewBag.Status = Enum.GetValues(typeof(StatusForDto)).Cast<StatusForDto>().Select(e => new SelectListItem
            {
                Value = e.ToString(),
                Text = e switch
                {
                    StatusForDto.Pending => "Kutilmoqda",
                    StatusForDto.Completed => "Yakunlangan",
                    StatusForDto.Rejected => "Rad etilgan",
                    StatusForDto.Unassigned => "Tayinlanmagan",
                    StatusForDto.RejectedByDriver => "Haydovchi tomonidan rad etilgan",
                    _ => "No`malum holat"
                }
            }).ToList();

            return View(review);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MechanicHandoverForUpdateDto mechanicHandover)
        {
            if (id != mechanicHandover.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await _mechanicHandoverDataStore.UpdateMechanicHandoverAsync(id, mechanicHandover);
                }
                catch (Exception)
                {
                    if (!await MechanicHandoverExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Drivers = new SelectList(await GETDrivers(), "Value", "Text");
            ViewBag.Cars = new SelectList(await GETCars(), "Value", "Text");

            return View(mechanicHandover);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var mechanicHandover = await _mechanicHandoverDataStore.GetMechanicHandoverAsync(id);
            if (mechanicHandover == null)
            {
                return NotFound();
            }
            return View(mechanicHandover);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _mechanicHandoverDataStore.DeleteMechanicHandoverAsync(id);
            return RedirectToAction(nameof(Index));
        }
        private async Task<bool> MechanicHandoverExists(int id)
        {
            var mechanicAcceptance = await _mechanicHandoverDataStore.GetMechanicHandoverAsync(id);
            return mechanicAcceptance != null;
        }
        private async Task<List<SelectListItem>> GETCars()
        {
            var carResponse = await _carDataStore.GetCarsAsync(1);
            var cars = carResponse.Data
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Model} ({c.Number})"
                })
                .ToList();
            return cars;
        }
        private async Task<List<SelectListItem>> GETDrivers()
        {
            var driverResponse = await _driverDataStore.GetDriversAsync(1);
            var drivers = driverResponse.Data
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = $"{d.FirstName} {d.LastName}"
                })
                .ToList();
            return drivers;
        }

        public async Task<IActionResult> Details(int id)
        {
            var mechanicHandover = await _mechanicHandoverDataStore.GetMechanicHandoverAsync(id);

            return View(mechanicHandover);
        }

        [HttpGet]
        public async Task<IActionResult> GetCarDetails(int carId)
        {
            var car = await _carDataStore.GetCarAsync(carId); 
            if (car != null)
            {
                var carDetails = new
                {
                    mileage = car.Mileage
                };
                return Json(carDetails);
            }
            return NotFound();
        }

    }
}