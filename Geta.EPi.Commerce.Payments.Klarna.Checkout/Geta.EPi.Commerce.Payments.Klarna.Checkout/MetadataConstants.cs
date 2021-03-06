﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geta.EPi.Commerce.Payments.Klarna.Checkout
{
    public static class MetadataConstants
    {
        [Obsolete("This field has been replaced by 'ReservationId'.")]
        public const string ReservationField = "Reservation";
        public const string ReservationId = "KlarnaReservationId";

        [Obsolete("This field has been replaced by 'InvoiceId'.")]
        public const string InvoiceNumber = "InvoiceNumber";
        public const string InvoiceId = "KlarnaInvoiceId";

        public const string OrderNamespace = "Mediachase.Commerce.Orders";
        public const string OtherPaymentClass = "OtherPayment";
        public const string PurchaseOrderClass = "PurchaseOrder";
        public const string VatPercent = "VatPercent";
    }
}
