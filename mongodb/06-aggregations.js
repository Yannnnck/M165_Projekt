print('Umsatz pro Event (nur bezahlt, CHF):');
printjson(db.bookings.aggregate([
    {$match:{status:'bezahlt'}},
    {$group:{_id:'$eventId',revenue:{$sum:{$multiply:['$quantity','$unitPrice']}},tickets:{$sum:'$quantity'}}},
    {$lookup:{from:'events',localField:'_id',foreignField:'_id',as:'event'}},
    {$unwind:'$event'},
    {$project:{_id:0,event:'$event.title',revenue:1,tickets:1}},
    {$sort:{revenue:-1,event:1}}
]).toArray());
print('Beliebteste Kategorie pro Stadt (reserviert + bezahlt):');
printjson(db.bookings.aggregate([
    {$match:{status:{$in:['reserviert','bezahlt']}}},
    {$lookup:{from:'events',localField:'eventId',foreignField:'_id',as:'event'}},
    {$unwind:'$event'},
    {$lookup:{from:'locations',localField:'event.locationId',foreignField:'_id',as:'location'}},
    {$unwind:'$location'},
    {$group:{_id:{city:'$location.address.city',category:'$category'},tickets:{$sum:'$quantity'}}},
    {$sort:{tickets:-1,'_id.city':1,'_id.category':1}}
]).toArray());
