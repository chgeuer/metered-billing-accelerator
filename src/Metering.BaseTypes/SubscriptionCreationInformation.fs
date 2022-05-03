﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Metering.BaseTypes

/// Event representing the creation of a subscription. 
type SubscriptionCreationInformation =
    { Subscription: Subscription // The purchase information of the subscription
      InternalMetersMapping: InternalMetersMapping } // The table mapping app-internal meter names to 'proper' ones for marketplace
        
module SubscriptionCreationInformation =
    let toStr { Subscription = s } : string =
        $"{s.SubscriptionStart |> MeteringDateTime.toStr}: SubscriptionCreation ID={s.InternalResourceId.ToString()} {s.RenewalInterval}"
