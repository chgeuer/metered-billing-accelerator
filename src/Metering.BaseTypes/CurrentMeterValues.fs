﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Metering.BaseTypes

type CurrentMeterValues = // Collects all meters per internal metering event type
    Map<DimensionId, MeterValue> 

module CurrentMeterValues =
    let toStr (cmv: CurrentMeterValues) =
        cmv
        |> Map.toSeq
        |> Seq.map (fun (k,v) -> 
            sprintf "%30s: %s" 
                (k.value)
                (v.ToString())
        )
