Library for Klarna Checkout integration with EPiServer Commerce
=============

![](http://tc.geta.no/app/rest/builds/buildType:(id:TeamFrederik_EPiCommercePaymentsKlarnaCheckout_Debug)/statusIcon)

## What is Geta.EPi.Commerce.Payments.Klarna.Checkout?

Geta.EPi.Commerce.Payments.Klarna.Checkout is library which helps to integrate Klarna Checkout as one of the payment options in your EPiServer Commerce sites.
This library consists of three assemblies: 
* Geta.Klarna.Checkout is wrapper for Klarna Checkout API and simplifies API usage 
* Geta.EPi.Commerce.Payments.Klarna.Checkout contains extensions and helpers for easier EPiServer Commerce and Klarna Checkout integration 
* Geta.EPi.Payments.Klarna.CommerceManager contains .ascx for payment method settings for Commerce Manager

## How to get started?

Start by installing NuGet packages (use [NuGet](http://nuget.episerver.com/)):

    Install-Package Geta.EPi.Commerce.Payments.Klarna.Checkout

For the Commerce Manager site run the following package:

    Install-Package Geta.EPi.Payments.Klarna.CommerceManager

## Setup

### Endpoints

Klarna Checkout requires four endpoints for checkout (Validate is optional):
- Checkout where you have to call Klarna Checkout and provide all Klarna's cart items including shipping information, locale of Klarna Checkout, URL's of all endponts mentioned here. Klarna API will return HTML snippet which you have to render on your page where user will fill all required details for Klarna. Here is an example:

```
public ActionResult KlarnaCheckout()
{
    var cart = GetCart();
    var cartItems = cart
        .GetAllLineItems()
        .Select(item => item.ToCartItem())
        .ToList();

    var shipment = cart.OrderForms[0].Shipments.FirstOrDefault();
    if (shipment == null)
    {
        throw new Exception("Shipment not selected. Shipment should be persisted into the cart before checkout.");
    }

    cartItems.Add(shipment.ToCartItem());

    
    var baseUri = GetBaseUri();
    var currentCheckoutPageUrl = string.Format("{0}{1}", baseUri, currentPage.PublicUrl());

    var checkoutUris = new CheckoutUris(
                new Uri(currentCheckoutPageUrl),
                new Uri(currentCheckoutPageUrl + "KlarnaConfirm"),
                new Uri(currentCheckoutPageUrl + "KlarnaPush"),
                new Uri(currentCheckoutPageUrl + "KlarnaTerms"),
                new Uri(currentCheckoutPageUrlAsHttps + "KlarnaValidate"); // validate url has to be https

	//Note: _checkoutClient variable should be created as private variable or property using Provider settings
	
	//var providerSettings = PaymentHelper.GetProviderSettings(currentMarket, currentLanguageBranch);
	//var _checkoutClient = new CheckoutClient(providerSettings.OrderBaseUri, 
					//providerSettings.MerchantId, 
					//providerSettings.Secret);
					
    var existingKlarnaOrderId = TempData["klarnaOrder"];
	
    var response = _checkoutClient.Checkout(cartItems, PaymentSettings.CurrentLocale, checkoutUris, existingKlarnaOrderId);

    var model = new KlarnaCheckoutView(CurrentPage)
    {
        Snippet = response.Snippet
		KlarnaTransactionId = response.TransactionId;
    };

    if (ControllerContext.IsChildAction)
    {
        return PartialView(model);
    }
    return View(model);
}
```

- Validate is called when the user completes the payment, but before the order is created in Klarnas system. Here you should validate that the cart has not been changed and that items in cart are in stock. If everyting is ok, simply return HttpStatusCode.Ok. If not, specify a url that Klarna should send the user back to:

```
[System.Web.Mvc.HttpPost]
 public ActionResult Validate()
 {
   var orderJson = new System.IO.StreamReader(Request.InputStream).ReadToEnd();
   var order = JsonConvert.DeserializeObject<KlarnaOrderJson>(orderJson);

   var id = order.Id;
   var cart = GetCart();
 
   int cartTotal = order.Cart.TotalPriceIncTax;
   // do the validation here - return 200 if all is good
   if(cartTotal == cart.Total && InStock(cart))
     return new HttpStatusCodeResult(HttpStatusCode.OK);

   var url = GetCartNotValidUrl(id);
   // specifying which url Klarna should redirect to - I'm using the CartNotValid action
   return Redirect(url);
 }
 
 public ActionResult CartNotValid(string klarnaOrder)
 {
   // user will be redirected here if validate is not OK
   TempData["klarnaOrder"] = klarnaOrder;
   TempData["cartNotValid"] = GetCartNotValidMessage();
   return RedirectToAction("KlarnaCheckout");
 }
 
```

- If Validate is ok, Confirm is the next step. In this step you should retrieve the Klarna order and render the HTML snippet on the page. This snippet contains information about payment confirmation. Example:

```
public ActionResult KlarnaConfirm(string klarnaOrder)
{
   var model = new KlarnaCheckoutViewModel();
   model.KlarnaTransactionId = klarnaOrder;
   if (string.IsNullOrEmpty(klarnaOrder))
       return RedirectToAction("Index");
  
  //Before proceeding, make sure cart has not been manipulated
  var klarnaOrderObject = CheckoutClient.GetOrder(klarnaOrder); 
  var cart = GetCart();
  if (!IsPaymentValidForCart(klarnaOrderObject.TotalCost, cart))
  {
       Logger.WarnFormat("Cart has changed, cart total is {0}, Klarna total is {1}." +
  "Redirecting back to checkout page.", cart.Total, ((decimal)klarnaOrderObject.TotalCost) / 100);
       return RedirectToAction("Index");
  }
  
  model.OrderNumber = OrderNumberGenerator.GetOrderNumber(cart);
  //rename cart and remove anonymous cookie
  RenameCartAndSaveChanges(myCartHelper, model.KlarnaTransactionId, 
  ControllerContext.HttpContext.User.Identity, Response);

  model.KlarnaHtmlSnippet = klarnaOrderObject.Snippet;
  return View(model);
}
```
```
private bool IsPaymentValidForCart(int klarnaTotal, Cart cart)
{
   decimal klarnaOrderCost = ((decimal)klarnaTotal) / 100;
   return klarnaOrderCost == cart.Total;
}
```

- Push is called from Klarna when order is confirmed, but status not updated to created. Klarna client's Confirm already sets status to created, but this is still required for Klarna Checkout. 
In this step you should create the order in your system. 
The ConfirmResponse object will contain all the data you need, data such as billing address and more. *It's important that the payment object contains meta data for klarna order id and reservation number* (see code below). The reservation number will be used for capturing payment when the order is shipped. Here is an example:

```
 public ActionResult KlarnaPush(string klarnaOrder)
 {
   var response = CheckoutClient.Confirm(klarnaOrder);
   // Order updated from status complete to created
   if (response.Status == Geta.Klarna.Checkout.Models.OrderStatus.Created)
   {
     // NOTE: When creating the Payment object, make sure to save klarna order id and reservation number
     // paymentObject.TransactionID = klarnaOrder;
     // paymentObject[MetadataConstants.ReservationField] = response.ReservationNumber;
     PrepareOrder(response);
   }
   else
   {
     var msg =
        string.Format(
           "KLARNA PUSH FAILED: Status is is {0} for transaction {1}.", 
     response.Status, klarnaOrder);
     Logger.ErrorFormat(msg);
   }
   return new HttpStatusCodeResult(HttpStatusCode.OK);
 }
```

- Terms is needed to display terms of your site in Klarna Checkout. It can be some MVC view or even static HTML file.

### Snippet rendering

Snippet is just string which contains HTML. To render it just call *@Html.Raw(Model.Snippet)*

### Configure Commerce Manager

Login into Commerce Manager and open **Administration -> Order System -> Payments**. Then click **New** and in **Overview** tab fill:

- **Name**
- **System Keyword** - use some Keyword which you can use later to find this payment method in your code
- **Language**
- **Class Name** - choose **Geta.EPi.Commerce.Payments.Klarna.Checkout.KlarnaCheckoutPaymentGateway**
- **Payment Class** - choose **Mediachase.Commerce.Orders.OtherPayment**
- **IsActive** - **Yes**
- select shipping methods available for this payment
- navigate to parameters tab and fill inn settings (see screenshot below)


![Payment method settings](/Geta.EPi.Commerce.Payments.Klarna.Checkout/screenshots/klarnaSettings.png?raw=true "Payment method settings")

![Payment method settings](/Geta.EPi.Commerce.Payments.Klarna.Checkout/screenshots/klarnaParameters.png?raw=true "Payment method parameters")

**Note: If the parameters tab is empty (or gateway class is missing), make sure you have installed the commerce manager nuget (see above)**

In **Markets** tab select market for which this payment will be available.

## Creating NuGet package

Project contains _pack.bat_ file for easier packaging. It calls _nuget.exe_ (assuming it's in your environment PATH) with _-IncludeReferencedProjects_ to include referenced Geta.Klarna.Checkout assembly. You also can provide output directory as a parameter for _pack.bat_.

## Troubleshooting tips

### Not receiving Push from Klarna
If you are not receiving push notifications from Klarna make sure the urls you send (when creating the checkout client) needs to be a public dns.

## More info

### Related blog posts

http://geta.no/blogg/a-major-facelift-for-the-geta-klarna-checkout-module/
http://geta.no/blogg/validating-a-checkout-order/

### Klarna Checkout API reference

https://developers.klarna.com/en/api-references-v1/klarna-checkout

### Klarna Checkout documentation

https://developers.klarna.com/en/klarna-checkout
