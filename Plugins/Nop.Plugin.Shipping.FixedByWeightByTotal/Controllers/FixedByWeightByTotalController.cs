using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using Nop.Core.Data;
using Nop.Core.Domain.Directory;
using Nop.Data;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Kendoui;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Controllers
{
    
    [Area(AreaNames.Admin)]
    public class FixedByWeightByTotalController : BasePluginController
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly FixedByWeightByTotalSettings _fixedByWeightByTotalSettings;
        private readonly ICountryService _countryService;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizationService _localizationService;
        private readonly IMeasureService _measureService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IShippingByWeightByTotalService _shippingByWeightService;
        private readonly IShippingService _shippingService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreService _storeService;
        private readonly MeasureSettings _measureSettings;
        private readonly IDbContext _dbContext;
        #endregion

        #region Ctor

        public FixedByWeightByTotalController(CurrencySettings currencySettings,
            FixedByWeightByTotalSettings fixedByWeightByTotalSettings,
            ICountryService countryService,
            ICurrencyService currencyService,
            ILocalizationService localizationService,
            IMeasureService measureService,
            IPermissionService permissionService,
            ISettingService settingService,
            IShippingByWeightByTotalService shippingByWeightService,
            IShippingService shippingService,
            IStateProvinceService stateProvinceService,
            IStoreService storeService,
            MeasureSettings measureSettings,
            IDbContext dbContext)
        {
            this._currencySettings = currencySettings;
            this._fixedByWeightByTotalSettings = fixedByWeightByTotalSettings;
            this._countryService = countryService;
            this._currencyService = currencyService;
            this._localizationService = localizationService;
            this._measureService = measureService;
            this._permissionService = permissionService;
            this._settingService = settingService;
            this._shippingByWeightService = shippingByWeightService;
            this._stateProvinceService = stateProvinceService;
            this._shippingService = shippingService;
            this._storeService = storeService;
            this._measureSettings = measureSettings;
            this._dbContext = dbContext;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                LimitMethodsToCreated = _fixedByWeightByTotalSettings.LimitMethodsToCreated,
                ShippingByWeightByTotalEnabled = _fixedByWeightByTotalSettings.ShippingByWeightByTotalEnabled
            };

            //stores
            model.AvailableStores.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var store in _storeService.GetAllStores())
                model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString() });
            //warehouses
            model.AvailableWarehouses.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var warehouses in _shippingService.GetAllWarehouses())
                model.AvailableWarehouses.Add(new SelectListItem { Text = warehouses.Name, Value = warehouses.Id.ToString() });
            //shipping methods
            foreach (var sm in _shippingService.GetAllShippingMethods())
                model.AvailableShippingMethods.Add(new SelectListItem { Text = sm.Name, Value = sm.Id.ToString() });
            //countries
            model.AvailableCountries.Add(new SelectListItem { Text = "*", Value = "0" });
            var countries = _countryService.GetAllCountries();
            foreach (var c in countries)
                model.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString() });
            //states
            model.AvailableStates.Add(new SelectListItem { Text = "*", Value = "0" });

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAntiForgery]
        [AuthorizeAdmin]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return Content("Access denied");

            //save settings
            _fixedByWeightByTotalSettings.LimitMethodsToCreated = model.LimitMethodsToCreated;
            _settingService.SaveSetting(_fixedByWeightByTotalSettings);

            return Json(new { Result = true });
        }

        [HttpPost]
        [AuthorizeAdmin]
        public IActionResult SaveMode(bool value)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return Content("Access denied");

            //save settings
            _fixedByWeightByTotalSettings.ShippingByWeightByTotalEnabled = value;
            _settingService.SaveSetting(_fixedByWeightByTotalSettings);

            return Json(new { Result = true });
        }

        #region Fixed rate

        [HttpPost]
        [AuthorizeAdmin]
        public IActionResult FixedShippingRateList(DataSourceRequest command)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedKendoGridJson();

            var rateModels = _shippingService.GetAllShippingMethods().Select(shippingMethod => new FixedRateModel
            {
                ShippingMethodId = shippingMethod.Id,
                ShippingMethodName = shippingMethod.Name,
                Rate = _settingService.GetSettingByKey<decimal>(string.Format(FixedByWeightByTotalDefaults.FixedRateSettingsKey, shippingMethod.Id)),
                //ADDED FOR FREE SHIPPING
                IsFreeShipping = _settingService.GetSettingByKey<bool>(string.Format(FixedByWeightByTotalDefaults.FreeShippingSettingsKey, shippingMethod.Id))
            }).ToList();

            var gridModel = new DataSourceResult
            {
                Data = rateModels,
                Total = rateModels.Count
            };

            return Json(gridModel);
        }

        [HttpPost]
        [AdminAntiForgery]
        [AuthorizeAdmin]
        public IActionResult UpdateFixedShippingRate(FixedRateModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return Content("Access denied");

            _settingService.SetSetting(string.Format(FixedByWeightByTotalDefaults.FixedRateSettingsKey, model.ShippingMethodId), model.Rate);

            //ADDED FOR FREE SHIPPING
            _settingService.SetSetting(string.Format(FixedByWeightByTotalDefaults.FreeShippingSettingsKey, model.ShippingMethodId), model.IsFreeShipping);


            return new NullJsonResult();
        }

        #endregion

        #region Rate by weight

        [HttpPost]
        [AdminAntiForgery]
        [AuthorizeAdmin]
        public IActionResult RateByWeightByTotalList(DataSourceRequest command, ConfigurationModel filter)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedKendoGridJson();

            //var records = _shippingByWeightService.GetAll(command.Page - 1, command.PageSize);
            var records = _shippingByWeightService.FindRecords(
              pageIndex: command.Page - 1,
              pageSize: command.PageSize,
              storeId: filter.SearchStoreId,
              warehouseId: filter.SearchWarehouseId,
              countryId: filter.SearchCountryId,
              stateProvinceId: filter.SearchStateProvinceId,
              zip: filter.SearchZip,
              shippingMethodId: filter.SearchShippingMethodId,
              weight: null,
              orderSubtotal: null
              );

            var sbwModel = records.Select(record =>
            {
                var model = new ShippingByWeightByTotalModel
                {
                    Id = record.Id,
                    StoreId = record.StoreId,
                    StoreName = _storeService.GetStoreById(record.StoreId)?.Name ?? "*",
                    WarehouseId = record.WarehouseId,
                    WarehouseName = _shippingService.GetWarehouseById(record.WarehouseId)?.Name ?? "*",
                    ShippingMethodId = record.ShippingMethodId,
                    ShippingMethodName = _shippingService.GetShippingMethodById(record.ShippingMethodId)?.Name ?? "Unavailable",
                    CountryId = record.CountryId,
                    CountryName = _countryService.GetCountryById(record.CountryId)?.Name ?? "*",
                    StateProvinceId = record.StateProvinceId,
                    StateProvinceName = _stateProvinceService.GetStateProvinceById(record.StateProvinceId)?.Name ?? "*",
                    WeightFrom = record.WeightFrom,
                    WeightTo = record.WeightTo,
                    OrderSubtotalFrom = record.OrderSubtotalFrom,
                    OrderSubtotalTo = record.OrderSubtotalTo,
                    AdditionalFixedCost = record.AdditionalFixedCost,
                    PercentageRateOfSubtotal = record.PercentageRateOfSubtotal,
                    RatePerWeightUnit = record.RatePerWeightUnit,
                    LowerWeightLimit = record.LowerWeightLimit,
                    Zip = !string.IsNullOrEmpty(record.Zip) ? record.Zip : "*"
                };                

                var htmlSb = new StringBuilder("<div>");
                htmlSb.AppendFormat("{0}: {1}", _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.WeightFrom"), model.WeightFrom);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}", _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.WeightTo"), model.WeightTo);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}", _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalFrom"), model.OrderSubtotalFrom);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}", _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalTo"), model.OrderSubtotalTo);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}", _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.AdditionalFixedCost"), model.AdditionalFixedCost);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}", _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.RatePerWeightUnit"), model.RatePerWeightUnit);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}", _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.LowerWeightLimit"), model.LowerWeightLimit);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}", _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.PercentageRateOfSubtotal"), model.PercentageRateOfSubtotal);

                htmlSb.Append("</div>");
                model.DataHtml = htmlSb.ToString();

                return model;
            }).ToList();

            var gridModel = new DataSourceResult
            {
                Data = sbwModel,
                Total = records.TotalCount
            };

            return Json(gridModel);
        }

        [AuthorizeAdmin]
        public IActionResult AddRateByWeightByTotalPopup()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();

            var model = new ShippingByWeightByTotalModel
            {
                PrimaryStoreCurrencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode,
                BaseWeightIn = _measureService.GetMeasureWeightById(_measureSettings.BaseWeightId)?.Name,
                WeightTo = 1000000,
                OrderSubtotalTo = 1000000
            };

            var shippingMethods = _shippingService.GetAllShippingMethods();
            if (!shippingMethods.Any())
                return Content("No shipping methods can be loaded");

            //stores
            model.AvailableStores.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var store in _storeService.GetAllStores())
                model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString() });
            //warehouses
            model.AvailableWarehouses.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var warehouses in _shippingService.GetAllWarehouses())
                model.AvailableWarehouses.Add(new SelectListItem { Text = warehouses.Name, Value = warehouses.Id.ToString() });
            //shipping methods
            foreach (var sm in shippingMethods)
                model.AvailableShippingMethods.Add(new SelectListItem { Text = sm.Name, Value = sm.Id.ToString() });
            //countries
            model.AvailableCountries.Add(new SelectListItem { Text = "*", Value = "0" });
            var countries = _countryService.GetAllCountries(showHidden: true);
            foreach (var c in countries)
                model.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString() });
            //states
            model.AvailableStates.Add(new SelectListItem { Text = "*", Value = "0" });

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/AddRateByWeightByTotalPopup.cshtml", model);
        }
        
        [HttpPost]
        [AdminAntiForgery]
        [AuthorizeAdmin]
        public IActionResult AddRateByWeightByTotalPopup(ShippingByWeightByTotalModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();
            
            _shippingByWeightService.InsertShippingByWeightRecord(new ShippingByWeightByTotalRecord
            {
                StoreId = model.StoreId,
                WarehouseId = model.WarehouseId,
                CountryId = model.CountryId,
                StateProvinceId = model.StateProvinceId,
                Zip = model.Zip == "*" ? null : model.Zip,
                ShippingMethodId = model.ShippingMethodId,
                WeightFrom = model.WeightFrom,
                WeightTo = model.WeightTo,
                OrderSubtotalFrom = model.OrderSubtotalFrom,
                OrderSubtotalTo = model.OrderSubtotalTo,
                AdditionalFixedCost = model.AdditionalFixedCost,
                RatePerWeightUnit = model.RatePerWeightUnit,
                PercentageRateOfSubtotal = model.PercentageRateOfSubtotal,
                LowerWeightLimit = model.LowerWeightLimit
            });

            ViewBag.RefreshPage = true;

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/AddRateByWeightByTotalPopup.cshtml", model);
        }

        [AuthorizeAdmin]
        public IActionResult EditRateByWeightByTotalPopup(int id)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();

            var sbw = _shippingByWeightService.GetById(id);
            if (sbw == null)
                //no record found with the specified id
                return RedirectToAction("Configure");

            var model = new ShippingByWeightByTotalModel
            {
                Id = sbw.Id,
                StoreId = sbw.StoreId,
                WarehouseId = sbw.WarehouseId,
                CountryId = sbw.CountryId,
                StateProvinceId = sbw.StateProvinceId,
                Zip = sbw.Zip,
                ShippingMethodId = sbw.ShippingMethodId,
                WeightFrom = sbw.WeightFrom,
                WeightTo = sbw.WeightTo,
                OrderSubtotalFrom = sbw.OrderSubtotalFrom,
                OrderSubtotalTo = sbw.OrderSubtotalTo,
                AdditionalFixedCost = sbw.AdditionalFixedCost,
                PercentageRateOfSubtotal = sbw.PercentageRateOfSubtotal,
                RatePerWeightUnit = sbw.RatePerWeightUnit,
                LowerWeightLimit = sbw.LowerWeightLimit,
                PrimaryStoreCurrencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode,
                BaseWeightIn = _measureService.GetMeasureWeightById(_measureSettings.BaseWeightId)?.Name
            };

            var shippingMethods = _shippingService.GetAllShippingMethods();
            if (!shippingMethods.Any())
                return Content("No shipping methods can be loaded");

            var selectedStore = _storeService.GetStoreById(sbw.StoreId);
            var selectedWarehouse = _shippingService.GetWarehouseById(sbw.WarehouseId);
            var selectedShippingMethod = _shippingService.GetShippingMethodById(sbw.ShippingMethodId);
            var selectedCountry = _countryService.GetCountryById(sbw.CountryId);
            var selectedState = _stateProvinceService.GetStateProvinceById(sbw.StateProvinceId);
            //stores
            model.AvailableStores.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var store in _storeService.GetAllStores())
                model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString(), Selected = (selectedStore != null && store.Id == selectedStore.Id) });
            //warehouses
            model.AvailableWarehouses.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var warehouse in _shippingService.GetAllWarehouses())
                model.AvailableWarehouses.Add(new SelectListItem { Text = warehouse.Name, Value = warehouse.Id.ToString(), Selected = (selectedWarehouse != null && warehouse.Id == selectedWarehouse.Id) });
            //shipping methods
            foreach (var sm in shippingMethods)
                model.AvailableShippingMethods.Add(new SelectListItem { Text = sm.Name, Value = sm.Id.ToString(), Selected = (selectedShippingMethod != null && sm.Id == selectedShippingMethod.Id) });
            //countries
            model.AvailableCountries.Add(new SelectListItem { Text = "*", Value = "0" });
            var countries = _countryService.GetAllCountries(showHidden: true);
            foreach (var c in countries)
                model.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString(), Selected = (selectedCountry != null && c.Id == selectedCountry.Id) });
            //states
            var states = selectedCountry != null ? _stateProvinceService.GetStateProvincesByCountryId(selectedCountry.Id, showHidden: true).ToList() : new List<StateProvince>();
            model.AvailableStates.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var s in states)
                model.AvailableStates.Add(new SelectListItem { Text = s.Name, Value = s.Id.ToString(), Selected = (selectedState != null && s.Id == selectedState.Id) });

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/EditRateByWeightByTotalPopup.cshtml", model);
        }

        [HttpPost]
        [AdminAntiForgery]
        [AuthorizeAdmin]
        public IActionResult EditRateByWeightByTotalPopup(ShippingByWeightByTotalModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();

            var sbw = _shippingByWeightService.GetById(model.Id);
            if (sbw == null)
                //no record found with the specified id
                return RedirectToAction("Configure");

            sbw.StoreId = model.StoreId;
            sbw.WarehouseId = model.WarehouseId;
            sbw.CountryId = model.CountryId;
            sbw.StateProvinceId = model.StateProvinceId;
            sbw.Zip = model.Zip == "*" ? null : model.Zip;
            sbw.ShippingMethodId = model.ShippingMethodId;
            sbw.WeightFrom = model.WeightFrom;
            sbw.WeightTo = model.WeightTo;
            sbw.OrderSubtotalFrom = model.OrderSubtotalFrom;
            sbw.OrderSubtotalTo = model.OrderSubtotalTo;
            sbw.AdditionalFixedCost = model.AdditionalFixedCost;
            sbw.RatePerWeightUnit = model.RatePerWeightUnit;
            sbw.PercentageRateOfSubtotal = model.PercentageRateOfSubtotal;
            sbw.LowerWeightLimit = model.LowerWeightLimit;

            _shippingByWeightService.UpdateShippingByWeightRecord(sbw);

            ViewBag.RefreshPage = true;

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/EditRateByWeightByTotalPopup.cshtml", model);
        }

        [HttpPost]
        [AdminAntiForgery]
        [AuthorizeAdmin]
        public IActionResult DeleteRateByWeightByTotal(int id)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return Content("Access denied");

            var sbw = _shippingByWeightService.GetById(id);
            if (sbw != null)
                _shippingByWeightService.DeleteShippingByWeightRecord(sbw);

            return new NullJsonResult();
        }

        #endregion

        #endregion



        #region BOXIT DATA MANAGE


        public IActionResult SaveBoxitData(string pickupnameValue, string pickupadressValue, string pickupidValue)
        {
            List<SelectListItem> ObjSelectListItemList = new List<SelectListItem>();
            bool flag = true;
            try
            {
                ObjSelectListItemList.Add(new SelectListItem
                {
                    Text = "pickupnameValue",
                    Value = pickupnameValue,
                });

                ObjSelectListItemList.Add(new SelectListItem
                {
                    Text = "pickupadressValue",
                    Value = pickupadressValue,
                });

                ObjSelectListItemList.Add(new SelectListItem
                {
                    Text = "pickupidValue",
                    Value = pickupidValue,
                });

                HttpContext.Session.SetString("OrderBoxitData", "");
                HttpContext.Session.SetString("OrderBoxitData", JsonConvert.SerializeObject(ObjSelectListItemList));

                //Session["OrderBoxitData"] = null;
                //Session["OrderBoxitData"] = ObjSelectListItemList;
            }
            catch (Exception ex)
            {
                flag = false;
            }
            return Json(flag);
        }


        public IActionResult SaveBoxitToDatabase(string orderId)
        {
            bool flag = true;
            try
            {
                int intOrderId = 0;
                int.TryParse(orderId, out intOrderId);
                if (intOrderId != 0)
                {
                    var OrderBoxitDataSession = HttpContext.Session.GetString("OrderBoxitData");



                    if (OrderBoxitDataSession != null && OrderBoxitDataSession != "")
                    {


                        List<SelectListItem> objSelectListItemListBoxitData = new List<SelectListItem>();
                        //objSelectListItemListBoxitData = (List<SelectListItem>)Session["OrderBoxitData"];
                        objSelectListItemListBoxitData = JsonConvert.DeserializeObject<List<SelectListItem>>(OrderBoxitDataSession);

                        if (objSelectListItemListBoxitData != null)
                        {
                            string BoxitPickupNameValue = "";
                            string BoxitPickupAdressValue = "";
                            string BoxitPickupIdValue = "";

                            if (objSelectListItemListBoxitData.Where(s => s.Text == "pickupnameValue").Any())
                            {
                                BoxitPickupNameValue = objSelectListItemListBoxitData.Where(s => s.Text == "pickupnameValue").FirstOrDefault().Value;
                            }

                            if (objSelectListItemListBoxitData.Where(s => s.Text == "pickupadressValue").Any())
                            {
                                BoxitPickupAdressValue = objSelectListItemListBoxitData.Where(s => s.Text == "pickupadressValue").FirstOrDefault().Value;
                            }

                            if (objSelectListItemListBoxitData.Where(s => s.Text == "pickupidValue").Any())
                            {
                                BoxitPickupIdValue = objSelectListItemListBoxitData.Where(s => s.Text == "pickupidValue").FirstOrDefault().Value;
                            }

                            string UpdateBoxitDataToOrderTable = "UPDATE  [dbo].[Order] SET BoxitPickupName = N'" + BoxitPickupNameValue + "',BoxitPickupAdress = N'" + BoxitPickupAdressValue + "',BoxitPickupId = N'" + BoxitPickupIdValue + "' WHERE Id = " + intOrderId + "";
                            this._dbContext.ExecuteSqlCommand(UpdateBoxitDataToOrderTable, false, null, new object[0]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                flag = false;
            }
            //Session["OrderBoxitData"] = null;
            HttpContext.Session.SetString("OrderBoxitData", "");
            return Json(flag);
        }


        public IActionResult GetBoxitData(string orderId)
        {
            OrderBoxItData objOrderBoxItData = new OrderBoxItData();
            try
            {
                objOrderBoxItData.OrderId = orderId;

                string BoxitId = GetBoxItId(orderId);
                if (BoxitId != null)
                {
                    objOrderBoxItData.BoxitId = BoxitId;

                }

                string BoxitName = GetBoxitPickupName(orderId);
                if (BoxitName != null)
                {
                    objOrderBoxItData.BoxitName = BoxitName;

                }

                string BoxitAddress = GetBoxitPickupAdress(orderId);
                if (BoxitAddress != null)
                {
                    objOrderBoxItData.BoxitAddress = BoxitAddress;
                }
            }
            catch (Exception ex)
            {
                objOrderBoxItData = new OrderBoxItData();
            }
            return Json(objOrderBoxItData);

        }

        public string GetBoxItId(string orderId)
        {
            string stringBoxItId = "";

            try
            {
                var dataSettings = DataSettingsManager.LoadSettings();
                string connectionString = dataSettings.DataConnectionString;
                DataTable dt = new DataTable();

                string sql = "SELECT ISNULL(BoxitPickupId,'') as BoxitPickupId FROM [dbo].[Order] WHERE Id =" + orderId + "";

                using (SqlConnection sqlConn = new SqlConnection(connectionString))
                {
                    using (SqlCommand sqlCmd = new SqlCommand(sql, sqlConn))
                    {
                        sqlCmd.CommandType = CommandType.Text;

                        sqlConn.Open();
                        SqlDataReader reader = sqlCmd.ExecuteReader();

                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                stringBoxItId = reader["BoxitPickupId"].ToString();
                            }
                        }

                        reader.Close();
                        sqlConn.Close();
                    }
                }

            }
            catch (Exception ex)
            {
                stringBoxItId = "";
            }

            return stringBoxItId;
        }


        public string GetBoxitPickupName(string orderId)
        {
            string stringBoxitPickupName = "";

            try
            {
                var dataSettings = DataSettingsManager.LoadSettings();
                string connectionString = dataSettings.DataConnectionString;
                DataTable dt = new DataTable();

                string sql = "SELECT ISNULL(BoxitPickupName,'') as BoxitPickupName FROM [dbo].[Order] WHERE Id =" + orderId + "";

                using (SqlConnection sqlConn = new SqlConnection(connectionString))
                {

                    using (SqlCommand sqlCmd = new SqlCommand(sql, sqlConn))
                    {
                        sqlCmd.CommandType = CommandType.Text;

                        sqlConn.Open();
                        SqlDataReader reader = sqlCmd.ExecuteReader();

                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                stringBoxitPickupName = reader["BoxitPickupName"].ToString();
                            }
                        }

                        reader.Close();
                        sqlConn.Close();
                    }
                }

            }
            catch (Exception)
            {
                stringBoxitPickupName = "";
            }

            return stringBoxitPickupName;
        }

        public string GetBoxitPickupAdress(string orderId)
        {
            string stringBoxitPickupAdress = "";

            try
            {
                var dataSettings = DataSettingsManager.LoadSettings();
                string connectionString = dataSettings.DataConnectionString;
                DataTable dt = new DataTable();

                string sql = "SELECT ISNULL(BoxitPickupAdress,'') as BoxitPickupAdress FROM [dbo].[Order] WHERE Id =" + orderId + "";

                using (SqlConnection sqlConn = new SqlConnection(connectionString))
                {

                    using (SqlCommand sqlCmd = new SqlCommand(sql, sqlConn))
                    {
                        sqlCmd.CommandType = CommandType.Text;

                        sqlConn.Open();
                        SqlDataReader reader = sqlCmd.ExecuteReader();

                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                stringBoxitPickupAdress = reader["BoxitPickupAdress"].ToString();
                            }
                        }

                        reader.Close();
                        sqlConn.Close();
                    }
                }

            }
            catch (Exception)
            {
                stringBoxitPickupAdress = "";
            }

            return stringBoxitPickupAdress;
        }


        public class OrderBoxItData
        {
            public string OrderId { get; set; }
            public string BoxitId { get; set; }
            public string BoxitName { get; set; }
            public string BoxitAddress { get; set; }
        }

        #endregion

    }
}