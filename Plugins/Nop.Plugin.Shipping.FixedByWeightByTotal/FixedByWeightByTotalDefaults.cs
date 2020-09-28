
namespace Nop.Plugin.Shipping.FixedByWeightByTotal
{
    /// <summary>
    /// Represents constants of the "Fixed or by weight" shipping plugin
    /// </summary>
    public static class FixedByWeightByTotalDefaults
    {
        /// <summary>
        /// The key of the settings to save fixed rate of the shipping method
        /// </summary>
        public const string FixedRateSettingsKey = "ShippingRateComputationMethod.FixedByWeightByTotal.Rate.ShippingMethodId{0}";

        //ADDED FOR FREE SHIPPING
        public const string FreeShippingSettingsKey = "ShippingRateComputationMethod.FixedByWeightByTotal.FreeShipping.ShippingMethodId{0}";
    }
}
