﻿using CheckDrive.ApiContracts.Car;
using CheckDrive.ApiContracts.Dispatcher;
using CheckDrive.ApiContracts.DispatcherReview;
using CheckDrive.Web.Models;
using CheckDrive.Web.Stores.Cars;
using CheckDrive.Web.Stores.DispatcherReviews;
using CheckDrive.Web.Stores.Dispatchers;
using CheckDrive.Web.Stores.Drivers;
using CheckDrive.Web.Stores.MechanicAcceptances;
using CheckDrive.Web.Stores.MechanicHandovers;
using CheckDrive.Web.Stores.OperatorReviews;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Syncfusion.EJ2.Grids;

namespace CheckDrive.Web.Controllers
{
    public class DispatcherReviewsController : Controller
    {
        private readonly IDispatcherReviewDataStore _dispatcherReviewDataStore;
        private readonly IMechanicAcceptanceDataStore _mechanicAcceptanceDataStore;
        private readonly IMechanicHandoverDataStore _mechanicHandoverDataStore;
        private readonly IOperatorReviewDataStore _operatorDataStore;
        private readonly IDispatcherDataStore _dispatcherDataStore;
        private readonly IDriverDataStore _driverDataStore;
        private readonly ICarDataStore _carDataStore;

        public DispatcherReviewsController(
            IDispatcherReviewDataStore dispatcherReviewDataStore,
            IMechanicAcceptanceDataStore mechanicAcceptanceDataStore,
            IOperatorReviewDataStore operatorDataStore,
            IMechanicHandoverDataStore mechanicHandoverDataStore,
            IDispatcherDataStore dispatcherDataStore,
            IDriverDataStore driverDataStore,
            ICarDataStore carDataStore)
        {
            _dispatcherReviewDataStore = dispatcherReviewDataStore;
            _mechanicAcceptanceDataStore = mechanicAcceptanceDataStore;
            _operatorDataStore = operatorDataStore;
            _mechanicHandoverDataStore = mechanicHandoverDataStore;
            _driverDataStore = driverDataStore;
            _carDataStore = carDataStore;
            _dispatcherDataStore = dispatcherDataStore;
        }

        public async Task<IActionResult> Index(int? pagenumber, string? searchString, DateTime? date)
        {
            var response = await _dispatcherReviewDataStore.GetDispatcherReviews(pagenumber, searchString, date, 1);


            if (response is null)
            {
                return BadRequest();
            }
            ViewBag.PageSize = response.PageSize;
            ViewBag.PageCount = response.TotalPages;
            ViewBag.TotalCount = response.TotalCount;
            ViewBag.CurrentPage = response.PageNumber;
            ViewBag.HasPreviousPage = response.HasPreviousPage;
            ViewBag.HasNextPage = response.HasNextPage;

            var dispatcherReviewResponse = response.Data.Select(r => new
            {
                r.Id,
                FuelSpended = r.FuelSpended.ToString("0.00").PadLeft(4, '0'),
                r.DistanceCovered,
                r.Date,
                r.CarMeduimFuelConsumption,
                r.CarName,
                r.DispatcherName,
                r.MechanicName,
                r.OperatorName,
                r.DriverName
            }).ToList();

            ViewBag.DispatcherReviews = dispatcherReviewResponse;
            return View();
        }

        public async Task<IActionResult> PersonalIndex(int? pagenumber, string? searchString)
        {
            var reviewsResponse = await _dispatcherReviewDataStore.GetDispatcherReviews(pagenumber, searchString, null, 5);

            ViewBag.PageSize = reviewsResponse.PageSize;
            ViewBag.PageCount = reviewsResponse.TotalPages;
            ViewBag.TotalCount = reviewsResponse.TotalCount;
            ViewBag.CurrentPage = reviewsResponse.PageNumber;
            ViewBag.HasPreviousPage = reviewsResponse.HasPreviousPage;
            ViewBag.HasNextPage = reviewsResponse.HasNextPage;

            return View(reviewsResponse.Data);
        }

        public async Task<IActionResult> Details(int id)
        {
            var review = await _dispatcherReviewDataStore.GetDispatcherReview(id);

            return View(review);
        }

        public async Task<IActionResult> Create(double? distanceCovered, double? fuelSpended, int operatorId, int mechanicId, int driverId, int mechanicHandoverId, int mechanicAcceptanceId, int carId, int operatorReviewId)
        {
            var accountIdStr = TempData["AccountId"] as string;
            TempData.Keep("AccountId");
            var dispatcher = new DispatcherDto();
            if (int.TryParse(accountIdStr, out int accountId))
            {
                var dispatcherResponse = await _dispatcherDataStore.GetDispatchers(accountId);
                dispatcher = dispatcherResponse.Data.First();
            }

            var model = new DispatcherReviewForCreateDto
            {
                DistanceCovered = distanceCovered ?? 0,
                FuelSpended = fuelSpended ?? 0,
                Date = DateTime.Now,
                DispatcherId = dispatcher.Id,
                OperatorId = operatorId,
                MechanicId = mechanicId,
                DriverId = driverId,
                CarId = carId,
                MechanicAcceptanceId = mechanicAcceptanceId,
                MechanicHandoverId = mechanicHandoverId,
                OperatorReviewId = operatorReviewId
            };
            var car = _carDataStore.GetCarAsync(carId);
            var fuelRemaining = car.Result.RemainingFuel;
            ViewBag.FuelRemaining = fuelRemaining;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FuelSpended,DistanceCovered,Date,DispatcherId,OperatorId,MechanicId,DriverId,MechanicHandoverId,MechanicAcceptanceId,CarId, OperatorReviewId")] DispatcherReviewForCreateDto dispatcherReview)
        {
            dispatcherReview.Date = DateTime.Now;
            var car = _carDataStore.GetCarAsync(dispatcherReview.CarId);
            var carr = new CarForUpdateDto
            {
                Id = dispatcherReview.CarId,
                Color = car.Result.Color,
                FuelTankCapacity = car.Result.FuelTankCapacity,
                ManufacturedYear = car.Result.ManufacturedYear,
                MeduimFuelConsumption = car.Result.MeduimFuelConsumption,
                Model = car.Result.Model,
                Number = car.Result.Number,
                RemainingFuel = car.Result.RemainingFuel - dispatcherReview.FuelSpended,
            };
            if (ModelState.IsValid)
            {
                await _carDataStore.UpdateCarAsync(dispatcherReview.CarId, carr);
                await _dispatcherReviewDataStore.CreateDispatcherReview(dispatcherReview);
                return RedirectToAction(nameof(PersonalIndex));
            }
            var care = _carDataStore.GetCarAsync(dispatcherReview.CarId);
            var fuelRemaining = care.Result.RemainingFuel;
            ViewBag.FuelRemaining = fuelRemaining;
            return View(dispatcherReview);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var review = await _dispatcherReviewDataStore.GetDispatcherReview(id);

            if (review == null)
            {
                return NotFound();
            }

            var drivers = await _driverDataStore.GetDriversAsync(1);
            var cars = await _carDataStore.GetCarsAsync(1);
            var dispatchers = await _dispatcherDataStore.GetDispatchers();

            ViewBag.DispatcherSelectList = new SelectList(dispatchers.Data.Select(dispatcher => new
            {
                Id = dispatcher.Id,
                DisplayText = $"{dispatcher.FirstName} {dispatcher.LastName}"
            }), "Id", "DisplayText");

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


            return View(review);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DispatcherReviewForUpdateDto dispatcherReview)
        {
            if (id != dispatcherReview.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingReview = await _dispatcherReviewDataStore.GetDispatcherReview(dispatcherReview.Id);
                    var car = await _carDataStore.GetCarAsync(dispatcherReview.CarId);
                    var carr = new CarForUpdateDto
                    {
                        Id = dispatcherReview.CarId,
                        Color = car.Color,
                        FuelTankCapacity = car.FuelTankCapacity,
                        ManufacturedYear = car.ManufacturedYear,
                        MeduimFuelConsumption = car.MeduimFuelConsumption,
                        Model = car.Model,
                        Number = car.Number,
                        RemainingFuel = car.RemainingFuel + (double)existingReview.FuelSpended - dispatcherReview.FuelSpended,
                    };
                    var oldDispatcherReview = await _dispatcherReviewDataStore.GetDispatcherReview(id);
                    dispatcherReview.MechanicId = oldDispatcherReview.MechanicId;
                    dispatcherReview.MechanicAcceptanceId = oldDispatcherReview.MechanicAcceptanceId;
                    dispatcherReview.Date = oldDispatcherReview.Date;
                    dispatcherReview.OperatorReviewId = oldDispatcherReview.OperatorReviewId;
                    dispatcherReview.OperatorId = oldDispatcherReview.OperatorId;
                    dispatcherReview.MechanicHandoverId = oldDispatcherReview.MechanicHandoverId;
                    
                    var dr = await _dispatcherReviewDataStore.UpdateDispatcherReview(id, dispatcherReview);
                    await _carDataStore.UpdateCarAsync(carr.Id, carr);
                }
                catch (Exception ex)
                {
                    if (!await DispatcherReviewExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Drivers = new SelectList(await GETDrivers(), "Value", "Text");
            ViewBag.Cars = new SelectList(await GETCars(), "Value", "Text");

            return View(dispatcherReview);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var review = await _dispatcherReviewDataStore.GetDispatcherReview(id);
            if (review == null)
            {
                return NotFound();
            }
            return View(review);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _dispatcherReviewDataStore.DeleteDispatcherReview(id);
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> DispatcherReviewExists(int id)
        {
            var review = await _dispatcherReviewDataStore.GetDispatcherReview(id);
            return review != null;
        }

        private async Task<List<SelectListItem>> GETCars()
        {
            var carResponse = await _carDataStore.GetCarsAsync(null, null);
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
            var driverResponse = await _driverDataStore.GetDriversAsync(null, null);
            var drivers = driverResponse.Data
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = $"{d.FirstName} {d.LastName}"
                })
                .ToList();
            return drivers;
        }
    }
}
