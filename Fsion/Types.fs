﻿namespace Fsion

open System

[<Struct>]
type Text =
    internal
    | Text of string
    static member (+)(Text t1,Text t2) = Text(t1+t2)
    static member (+)(s:string,Text t) = Text(s+t)
    static member (+)(Text t,s:string) = Text(t+s)

module Text =
    let ofString s =
        if String.IsNullOrWhiteSpace s then None
        else s.Trim() |> Text |> Some
    let toString (Text s) = s
    let length (Text s) = s.Length

[<AutoOpen>]
module Auto =
    let inline mapFst f (a,b) = f a,b
    let inline fst3 (i,_,_) = i
    let inline snd3 (_,i,_) = i
    let inline trd (_,_,i) = i
    let (<*>) = Result.apply
    let inline zigzag (i:int) = (i <<< 1) ^^^ (i >>> 31) |> uint32
    let inline unzigzag (i:uint32) = int(i >>> 1) ^^^ -int(i &&& 1u)
    let inline zigzag64 (i:int64) = (i <<< 1) ^^^ (i >>> 63) |> uint64
    let inline unzigzag64 (i:uint64) = int64(i >>> 1) ^^^ -int64(i &&& 1UL)
    let private someunit = Some()
    let (|IsText|_|) str (Text s) =
        if String.Equals(str, s, StringComparison.OrdinalIgnoreCase) then
            someunit
        else None
    let tryCast (o:obj) : 'a option = // TODO: maybe should be Option.tryCast
        match o with
        | :? 'a as a -> Some a
        | _ -> None

[<Struct>]
type Tx =
    | Tx of uint32
    member m.Int =
        let (Tx i) = m
        i
    static member (-)(Tx t1,Tx t2) = int(t1-t2)


module Tx =
    let maxValue = Tx UInt32.MaxValue
    let next (Tx i) = Tx (i+1u)

[<Struct>]
type EntityType =
    | EntityType of uint32

module EntityType =
    module Int =
        [<Literal>]
        let transaction = 0u
        [<Literal>]
        let entityType = 1u
        [<Literal>]
        let attribute = 1u
    let transaction = EntityType Int.transaction
    let entityType = EntityType Int.entityType
    let attribute = EntityType Int.attribute

[<Struct>]
type Entity =
    | Entity of EntityType * uint32

[<Struct>]
type AttributeId =
    | AttributeId of uint32

module AttributeId =
    module Int =
        [<Literal>]
        let uri = 0u
        [<Literal>]
        let name = 1u
        [<Literal>]
        let time = 2u
        [<Literal>]
        let attribute_type = 3u
        [<Literal>]
        let attribute_isset = 4u
        [<Literal>]
        let transaction_based_on = 5u
    let uri = AttributeId Int.uri
    let name = AttributeId Int.name
    let time = AttributeId Int.time
    let attribute_type = AttributeId Int.attribute_type
    let attribute_isset = AttributeId Int.attribute_isset
    let transaction_based_on = AttributeId Int.transaction_based_on


[<Struct;CustomEquality;CustomComparison>]
type EntityAttribute =
    | EntityAttribute of Entity * AttributeId
    interface IEquatable<EntityAttribute> with
        member m.Equals (EntityAttribute(oe,oa)) =
            let (EntityAttribute(e,a)) = m
            oe = e && oa = a
    override m.Equals(o:obj) =
        match o with
        | :? EntityAttribute as ea ->
            let (EntityAttribute(oe,oa)) = m
            let (EntityAttribute(e,a)) = m
            oe = e && oa = a
        | _ -> false
    interface IComparable with
        member m.CompareTo o =
            match o with
            | :? EntityAttribute as ea ->
                let (EntityAttribute(oe,oa)) = m
                let (EntityAttribute(e,a)) = m
                0
            | _ -> 0
    override m.GetHashCode() =
        let (EntityAttribute(e,a)) = m
        e.GetHashCode() ^^^ a.GetHashCode()

[<Struct>]
type TextId =
    internal
    | TextId of uint32

[<Struct>]
type Data =
    internal
    | Data of byte[]

[<Struct>]
type DataId =
    internal
    | DataId of uint32

type Datum = Entity * AttributeId * Date * int64

type Transaction = {
    Text: Text list // set? Needs client to make unique for effiecient serialization
    Data: Data list
    Datum: Datum list1
} with
    member m.Tx =
        let Entity(_,txId),_,_,_ = m.Datum.Head
        Tx txId

[<Struct>]
type Uri =
    internal
    | Uri of uint32

[<Struct>]
type Counts =
    internal
    | Counts of uint32 array

module internal Counts =
    [<Struct>]
    type internal Edit =
        | Edit of uint32 array ref
    let get (EntityType ety) (Counts a) =
        if Array.length a > int ety+1 then a.[int ety+1] else 0u
    let getText (Counts a) = a.[0]
    let getData (Counts a) = a.[1]
    let emptyEdit() = Array.zeroCreate 2 |> ref |> Edit
    let toEdit (Counts a) = Array.copy a |> ref |> Edit
    let check (Edit a) (Entity(EntityType ety,eid)) =
        if ety <> EntityType.Int.transaction then
            if Array.length !a <= int ety+1 then Array.Resize(a, int ety+2)
            let a = !a
            if a.[int ety+1] <= eid then a.[int ety+1] <- eid + 1u
    let addText (Edit a) v =
        let a = !a
        a.[0] <- a.[0] + v
    let addData (Edit a) v =
        let a = !a
        a.[1] <- a.[1] + v
    let toCounts (Edit a) = Counts !a
    let empty = Counts [|0u;0u|]
    let update edit (txn:Transaction) =
        if List.isEmpty txn.Text |> not then addText edit (uint32(List.length txn.Text))
        if List.isEmpty txn.Data |> not then addData edit (uint32(List.length txn.Data))
        List1.iter (fun (ent,_,_,_) -> check edit ent) txn.Datum

[<AutoOpen>]
module Uri =
    
    let internal (|UriInt|UriNew|UriUri|UriInvalid|) (Text t,i,j) =
        
        let inline validateUri i j =
            let inline isLetter c = c>='a' && c<='z'
            let inline isNotLetter c = c<'a' || c>'z'
            let inline isNotDigit c = c>'9' || c<'0'
            let inline isUnderscore c = c='_'
            let rec check i prevUnderscore =
                if i = j then not prevUnderscore
                else
                    let c = t.[i]
                    if   isNotLetter c
                      && isNotDigit c 
                      && (prevUnderscore || not(isUnderscore c)) then false
                    else check (i+1) (isUnderscore c)
            isLetter t.[i] && check (i+1) false

        let inline validateInt i j =
            let inline isNotDigit c = c>'9' || c<'0'
            let rec check i =
                if i = j then true
                else
                    if isNotDigit t.[i] then false
                    else check (i+1)
            isNotDigit t.[i] |> not && check (i+1)

        let inline toInt i j =
            let rec calc n i =
                if i=j then n
                else calc (10u*n+(uint32 t.[i] - 48u)) (i+1)
            calc 0u i

        if validateInt i j then
            let n = toInt i j
            UriInt n
        elif t.[i]='n' && i+3<=j && t.[i+1]='e' && t.[i+2]='w'
          && validateInt (i+3) j then
            let n = toInt (i+3) j
            UriNew n
        elif validateUri i j then UriUri
        else UriInvalid