﻿module Fsion.Tests.DatabaseTests

open System
open Expecto
open Fsion

let dataCacheTestList (cache:DataCache) = [

        testList "dataSeries" [

            testAsync "set get" {
                let dataSeries = DataSeries.single (Date 1u,Tx 1u,1L)
                cache.Set (Entity(EntityType.attribute,1u), Attribute.time) dataSeries
                let actual = cache.Get (Entity(EntityType.attribute,1u), Attribute.time)
                Expect.equal actual (Some dataSeries) "bytes 1"
            }

            testAsync "ups" {
                let dataSeries = DataSeries.single (Date 1u,Tx 1u,1L)
                cache.Set (Entity(EntityType.attribute,2u), Attribute.time) dataSeries
                cache.Ups (Entity(EntityType.attribute,2u), Attribute.time) (Date 2u,Tx 2u,2L)
                let actual = cache.Get (Entity(EntityType.attribute,2u), Attribute.time)
                let expected = DataSeries.append (Date 2u,Tx 2u,2L) dataSeries |> Some
                Expect.equal actual expected "append"
            }
        ]

        testList "text" [

            testAsync "same id" {
                let expected = cache.GetTextId (Text.ofString "hi you")
                let actual = cache.GetTextId (Text.ofString " hi you ")
                Expect.equal actual expected "same id"
            }

            testAsync "diff id" {
                let expected = cache.GetTextId (Text.ofString "hi you")
                let actual = cache.GetTextId (Text.ofString "hi there")
                Expect.notEqual actual expected "diff id"
            }

            testAsync "case sensitive" {
                let expected = cache.GetTextId (Text.ofString "hi you")
                let actual = cache.GetTextId (Text.ofString "hi You")
                Expect.notEqual actual expected "case"
            }
        
            testProp "roundtrip" (fun (strings:string[]) ->
                let expected = Array.Parallel.map Text.ofString strings
                let textIds = Array.Parallel.map cache.GetTextId expected
                let actual = Array.Parallel.map cache.GetText textIds
                Expect.equal actual expected "strings same"
            )
        ]

        testList "data" [

            testAsync "save" {
                let expected = [|1uy;3uy|]
                let dataId = cache.GetDataId expected
                let actual = cache.GetData dataId
                Expect.equal actual expected "same id"
            }

            testAsync "diff id" {
                let expected = cache.GetDataId [|1uy;3uy|]
                let actual = cache.GetDataId [|7uy;5uy|]
                Expect.notEqual actual expected "diff id"
            }

            testProp "roundtrip" (fun (bytes:byte[][]) ->
                let byteIds = Array.Parallel.map cache.GetDataId bytes
                let actual = Array.Parallel.map cache.GetData byteIds
                Expect.equal actual bytes "bytes same"
            )
        ]
    ]

let dataCacheTests =
    DataCache.createMemory "."
    |> dataCacheTestList
    |> testList "dataCache memory"

let databaseTestList (db:Database) = [
    
    testAsync "nothing" {
        let txData = {
            Headers = []
            Creates = []
            Updates = []
            Text = [|Text.ofString "hi"|]
            Data = [||]
        }
        Database.setTransaction txData (Time 1L) db
    }

    testAsync "create" {
        let txData = {
            Headers = []
            Creates = [Entity(EntityType.attribute,1u), Attribute.uri, Date 10u, 0L]
            Updates = []
            Text = [|Text.ofString "my_uri"|]
            Data = [||]
        }
        Database.setTransaction txData (Time 2L) db
    }

    testAsync "update" {
        let txData = {
            Headers = []
            Creates = []
            Updates = [Attribute.uri.Entity, Attribute.uri, Date 10u, 0L]
            Text = [|Text.ofString "my_uri2"|]
            Data = [||]
        }
        Database.setTransaction txData (Time 2L) db
    }
]

let databaseTests =
    Database.createMemory "."
    |> databaseTestList
    |> testList "dataSeriesBase memory"