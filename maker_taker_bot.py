# coding=utf-8

import random 
import sys
import math 

import hashlib
import hmac
import requests
import time
import urllib
from operator import itemgetter

import dateparser
import pytz

from datetime import datetime

openware_api_url = "https://www.quantaexchange.org/api/v2/peatio"
openware_ranger_url = "wss://www.quantaexchange.org/api/v2/ranger"
ApiKey = "9d833da4f5651cc4"
ApiKeySecret = "f9b5a592b79cc73e712d99179b246128"
TradingPair = "wikieth"
#TradingPair = "ethusd"

def date_to_milliseconds(date_str):
    """Convert UTC date to milliseconds

    If using offset strings add "UTC" to date string e.g. "now UTC", "11 hours ago UTC"

    See dateparse docs for formats http://dateparser.readthedocs.io/en/latest/

    :param date_str: date in readable format, i.e. "January 01, 2018", "11 hours ago UTC", "now UTC"
    :type date_str: str
    """
    # get epoch value in UTC
    epoch = datetime.utcfromtimestamp(0).replace(tzinfo=pytz.utc)
    # parse our date string
    d = dateparser.parse(date_str)
    # if the date is not timezone aware apply UTC timezone
    if d.tzinfo is None or d.tzinfo.utcoffset(d) is None:
        d = d.replace(tzinfo=pytz.utc)

    # return the difference in time
    return int((d - epoch).total_seconds() * 1000.0)


def interval_to_milliseconds(interval):
    """Convert a Openware interval string to milliseconds

    :param interval: Openware interval string, e.g.: 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 8h, 12h, 1d, 3d, 1w
    :type interval: str

    :return:
         int value of interval in milliseconds
         None if interval prefix is not a decimal integer
         None if interval suffix is not one of m, h, d, w

    """
    seconds_per_unit = {
        "m": 60,
        "h": 60 * 60,
        "d": 24 * 60 * 60,
        "w": 7 * 24 * 60 * 60,
    }
    try:
        return int(interval[:-1]) * seconds_per_unit[interval[-1]] * 1000
    except (ValueError, KeyError):
        return None


class OpenwareAPIException(Exception):

    def __init__(self, response):
        self.code = 0
        try:
            json_res = response.json()
        except ValueError:
            self.message = 'Invalid JSON error message from Openware: {}'.format(response.text)
        else:
            self.message = response
        #     self.code = json_res['code']
        #     self.message = json_res['msg']
        self.status_code = response.status_code
        self.response = response
        self.request = getattr(response, 'request', None)

    def __str__(self):  # pragma: no cover
        return 'APIError(code=%s): %s' % (self.code, self.message)


class OpenwareRequestException(Exception):
    def __init__(self, message):
        self.message = message

    def __str__(self):
        return 'OpenwareRequestException: %s' % self.message


class OpenwareOrderException(Exception):

    def __init__(self, code, message):
        self.code = code
        self.message = message

    def __str__(self):
        return 'OpenwareOrderException(code=%s): %s' % (self.code, self.message)


class OpenwareOrderMinAmountException(OpenwareOrderException):

    def __init__(self, value):
        message = "Amount must be a multiple of %s" % value
        super(OpenwareOrderMinAmountException, self).__init__(-1013, message)


class OpenwareOrderMinPriceException(OpenwareOrderException):

    def __init__(self, value):
        message = "Price must be at least %s" % value
        super(OpenwareOrderMinPriceException, self).__init__(-1013, message)


class OpenwareOrderMinTotalException(OpenwareOrderException):

    def __init__(self, value):
        message = "Total must be at least %s" % value
        super(OpenwareOrderMinTotalException, self).__init__(-1013, message)


class OpenwareOrderUnknownSymbolException(OpenwareOrderException):

    def __init__(self, value):
        message = "Unknown symbol %s" % value
        super(OpenwareOrderUnknownSymbolException, self).__init__(-1013, message)


class OpenwareOrderInactiveSymbolException(OpenwareOrderException):

    def __init__(self, value):
        message = "Attempting to trade an inactive symbol %s" % value
        super(OpenwareOrderInactiveSymbolException, self).__init__(-1013, message)


class OpenwareWithdrawException(Exception):
    def __init__(self, message):
        if message == u'参数异常':
            message = 'Withdraw to this address through the website first'
        self.message = message

    def __str__(self):
        return 'OpenwareWithdrawException: %s' % self.message


class OpenwareClient(object):

    ORDER_STATUS_NEW = 'NEW'
    ORDER_STATUS_PARTIALLY_FILLED = 'PARTIALLY_FILLED'
    ORDER_STATUS_FILLED = 'FILLED'
    ORDER_STATUS_CANCELED = 'CANCELED'
    ORDER_STATUS_PENDING_CANCEL = 'PENDING_CANCEL'
    ORDER_STATUS_REJECTED = 'REJECTED'
    ORDER_STATUS_EXPIRED = 'EXPIRED'

    KLINE_INTERVAL_1MINUTE = '1m'
    KLINE_INTERVAL_3MINUTE = '3m'
    KLINE_INTERVAL_5MINUTE = '5m'
    KLINE_INTERVAL_15MINUTE = '15m'
    KLINE_INTERVAL_30MINUTE = '30m'
    KLINE_INTERVAL_1HOUR = '1h'
    KLINE_INTERVAL_2HOUR = '2h'
    KLINE_INTERVAL_4HOUR = '4h'
    KLINE_INTERVAL_6HOUR = '6h'
    KLINE_INTERVAL_8HOUR = '8h'
    KLINE_INTERVAL_12HOUR = '12h'
    KLINE_INTERVAL_1DAY = '1d'
    KLINE_INTERVAL_3DAY = '3d'
    KLINE_INTERVAL_1WEEK = '1w'
    KLINE_INTERVAL_1MONTH = '1M'

    SIDE_BUY = 'buy'
    SIDE_SELL = 'sell'

    ORDER_TYPE_LIMIT = 'limit'
    ORDER_TYPE_MARKET = 'market'

    TIME_IN_FORCE_GTC = 'GTC'  # Good till cancelled
    TIME_IN_FORCE_IOC = 'IOC'  # Immediate or cancel
    TIME_IN_FORCE_FOK = 'FOK'  # Fill or kill

    ORDER_RESP_TYPE_ACK = 'ACK'
    ORDER_RESP_TYPE_RESULT = 'RESULT'
    ORDER_RESP_TYPE_FULL = 'FULL'

    # For accessing the data returned by Client.aggregate_trades().
    AGG_ID = 'a'
    AGG_PRICE = 'p'
    AGG_QUANTITY = 'q'
    AGG_FIRST_TRADE_ID = 'f'
    AGG_LAST_TRADE_ID = 'l'
    AGG_TIME = 'T'
    AGG_BUYER_MAKES = 'm'
    AGG_BEST_MATCH = 'M'

    def __init__(self, api_key, api_secret, api_url):
        """Openware API Client constructor
        """
        self.api_key = api_key
        self.api_secret = api_secret
        self.api_url = api_url
        self.session = self._init_session()
        self.version()

    def _init_session(self):

        timestamp = str(time.time() * 1000)
        signature = self._generate_signature(timestamp)
        session = requests.session()
        session.headers.update({'Accept': 'application/json',
                                'User-Agent': 'openware/python',
                                'X-Auth-Apikey': self.api_key,
                                'X-Auth-Nonce': timestamp,
                                'X-Auth-Signature': signature})
        return session
    
    def update_headers(self):
        
        timestamp = str(time.time() * 1000)
        signature = self._generate_signature(timestamp)
        self.session.headers.update({'Accept': 'application/json',
                                'User-Agent': 'openware/python',
                                'X-Auth-Apikey': self.api_key,
                                'X-Auth-Nonce': timestamp,
                                'X-Auth-Signature': signature})
        return self.session

    def _create_api_uri(self, path):
        return "%s%s" % (self.api_url, path)

    def _generate_signature(self, timestamp):
        query_string = "%s%s" % (timestamp, self.api_key)
        m = hmac.new(self.api_secret.encode('utf-8'), query_string.encode('utf-8'), hashlib.sha256)
        return m.hexdigest()

    def _request(self, method, uri, force_params=False, **kwargs):

        data = kwargs.get('data', None)
        if data and isinstance(data, dict):
            kwargs['data'] = data
        # if get request assign data array to params value for requests lib
        if data and (method == 'get' or force_params):
            kwargs['params'] = kwargs['data']
            del(kwargs['data'])
        self.update_headers()
        response = getattr(self.session, method)(uri, **kwargs)
        return self._handle_response(response)

    def _request_api(self, method, path, **kwargs):
        uri = self._create_api_uri(path)
        return self._request(method, uri, **kwargs)

    def _handle_response(self, response):
        
        if not str(response.status_code).startswith('2'):
            raise OpenwareAPIException(response)
        try:
            resp = response.json()
            return resp
        except ValueError:
            raise OpenwareRequestException('Invalid Response: %s' % response.text)

    def _get(self, path, **kwargs):
        return self._request_api('get', path, **kwargs)

    def _post(self, path, **kwargs):
        return self._request_api('post', path, **kwargs)

    def _put(self, path, **kwargs):
        return self._request_api('put', path, **kwargs)

    def _delete(self, path, **kwargs):
        #return self._request_api('delete', path, signed, version, **kwargs)
        return self._request_api('delete', path, **kwargs)

    def version(self):
        return self._get("/public/timestamp")

    def get_markets(self):
        return self._get('/public/markets')

    def get_currencies(self):
        return self._get('/public/currencies')

    def get_server_time(self):
        return self._get('/public/timestamp')
    
    def get_balances(self):
        return self._get('/account/balances')

    def get_trade_fee(self):
        return self._get('/public/trading_fees')
    
    def get_my_trades(self, **params):
        return self._get("/market/trades", data=params)
    
    def get_order_by_id(self, **params):
        id = params.get('id')
        result = self._get("/market/orders/{}".format(id))
        return result
    
    def get_order(self, **params):
        result = self._get("/market/orders", data=params)
        return result

    def get_snapshot(self):
        return self._get("/public/markets/" + TradingPair + "/depth")

    def get_tickers(self):
        return self._get("/public/markets/tickers")

    def get_deposit_address(self, currency):
        return self._get("/account/deposit_address/%s" % currency)
    
    def withdraw(self, **params):
        return self._post("/account/withdraws", data=params)

    def create_order(self, **params):
        """
        Send in a new order
        """
        return self._post('/market/orders', data=params)

    def order_market(self, **params):
        """
        Send in a new market order
        """
        params.update({
            'ord_type': self.ORDER_TYPE_MARKET
        })
        return self.create_order(**params)
    
    def order_limit(self, **params):
        """
        Send in a new market order
        """
        params.update({
            'ord_type': self.ORDER_TYPE_LIMIT
        })
        return self.create_order(**params)
    
    def order_market_buy(self, **params):
        """
        Send in a new market buy order
        """
        params.update({
            'side': self.SIDE_BUY
        })
        return self.order_market(**params)

    def order_limit_buy(self, **params):
        """
        Send in a new market buy order
        """
        params.update({
            'side': self.SIDE_BUY
        })
        return self.order_limit(**params)
    
    def order_market_sell(self, **params):
        """
        Send in a new market sell order
        """
        params.update({
            'side': self.SIDE_SELL
        })
        return self.order_market(**params)

    def order_limit_sell(self, **params):
        """
        Send in a new market sell order
        """
        params.update({
            'side': self.SIDE_SELL
        })
        return self.order_limit(**params)

    def cancel_order(self, **params):
        """
        Cancel order
        """
        id = params.get('id')
        resp = self._post('/market/orders/%s/cancel' % id)

        if resp:
            print("Order Canceled " + resp['market'] + " " 
                + str(resp['id']) + " " + resp['uuid'] 
                + " " + resp['state'] 
                + " price: " + resp['price'] 
                + " volume: " + resp['origin_volume'])
            return resp
        else:
            print("Can not deserialise order")
            return None

####################################################################
####################################################################
####################################################################

client = OpenwareClient(ApiKey, ApiKeySecret, openware_api_url)
NewOrders = []

#print(client.version())
print(client.get_server_time())

def PrintMarkets():
    print("Markets:")
    marketsJSON = client.get_markets()
    for row in marketsJSON:
        print("\t" + row['name'] + " MinPrice: " + str(row['min_price'])
        + " MaxPrice: " + str(row['max_price']) 
        + " MinAmount: " + str(row['min_amount'])
        + " PricePrecision: " + str(row['price_precision'])
        + " AmountPrecision: " + str(row['amount_precision']))

def PrintBalances():
    print("Assets:")
    balancesJSON = client.get_balances()
    for row in balancesJSON:
        print("\t" + row['currency'] + " balance: " + row['balance'] + " locked: " + row['locked'])

def PrintOrders():
    print("Orders:")
    ordersJSON = client.get_order()
    for order in ordersJSON:
        if order['market'] == TradingPair and order['ord_type'] == "limit":
            if order["state"] == "pending" or  order["state"] == "wait":
                print("\t" + order['market'] + " " + str(order['id'])
                    + " " + order['side'] + " price: " + order['price']
                    + " volume: " + order['origin_volume']
                    + " " + order['state'] 
                    + " " + order['ord_type'] 
                    + " TradesCount: " + str(order['trades_count']))

def CancelOrders():
    for order in NewOrders:
        if(order['state'] == "pending" or order['state'] == "wait"):
            resp = client.cancel_order(id=order['id'])
            if resp:
                NewOrders.remove(order)

def ExecuteOrder(orderType, orderVolume, orderPrice):
    side = orderType # sell or buy 
    volume = "{:.5f}".format(orderVolume)
    price = "{:.4f}".format(orderPrice)
    ordType = "limit"

    data = "{\"market\": \"" + TradingPair + "\", \"volume\": \"" + volume + "\", \"price\": \"" + price + "\", \"side\": \"" + side + "\", \"ord_type\": \"" + ordType + "\"}";
    resp = client._post("/market/orders", data=data)
    if resp:
        print("Order Executed [" + ordType + "] " + resp['market'] 
            + " " + str(resp['id']) 
            + " " + resp['uuid'] 
            + " " + resp['state'] 
            + " price: " + resp['price'] 
            + " volume: " + resp['origin_volume'])
        return resp
    else:
        print("Can not deserialise order")
        return None
    
def getMinAsk(asksList):
    return float(min(asksList, key = lambda x: float(x[0]))[0])

def getMaxBid(bidList):
    return float(max(bidList, key = lambda x: float(x[0]))[0])

def NextFloat(min, max):
    import random
    random = random.uniform(0, 1)
    diff = max - min
    r = random * diff
    return min + r

def IssueNewProposal():
    numOrders = 10
    askSpread = 0.01
    bidSpread = 0.01
    offset = 0.0001
    minVolume = 0.01
    maxVolume = 11.57000

    marketDepth = client.get_snapshot()
    tickers = client.get_tickers()

    lastPrice = float(tickers[TradingPair]['ticker']['last'])

    if(len(marketDepth['asks']) > 0 and len(marketDepth['bids']) < numOrders):
        minPriceSell = getMinAsk(marketDepth['asks'])
        
        for i in range(0, numOrders):
            volume = NextFloat(minVolume, maxVolume)
            price = NextFloat(((lastPrice - offset) - (numOrders * bidSpread)), (minPriceSell - offset))
            try:
                order = ExecuteOrder("buy", volume, price)
                if order is not None:
                    NewOrders.append(order)
            except Exception as e:
                print("ERROR couldn't execute BUY order. Price:" + str(price) + " volume: " + str(volume))

    elif len(marketDepth['bids']) == 0:
        volume = NextFloat(minVolume, maxVolume)
        price = NextFloat(((lastPrice - offset) - (numOrders * bidSpread)), (lastPrice - offset))
        try:
            order = ExecuteOrder("buy", volume, price)
            if order is not None:
                NewOrders.append(order)
        except:
            print("ERROR couldn't execute BUY order. Price:" + str(price) + " volume: " + str(volume))

    if(len(marketDepth['bids']) > 0 and len(marketDepth['asks']) < numOrders):
        maxPriceBuy = getMaxBid(marketDepth['bids'])

        for i in range(0, numOrders):
            volume = NextFloat(minVolume, maxVolume)
            price = NextFloat(maxPriceBuy + offset, maxPriceBuy + offset + (numOrders * askSpread))
            try:
                order = ExecuteOrder("sell", volume, price)
                if order is not None:
                    NewOrders.append(order)
            except:
                print("ERROR couldn't execute SELL order. Price:" + str(price) + " volume: " + str(volume))

    elif len(marketDepth['asks']) == 0:
        volume = NextFloat(minVolume, maxVolume)
        price = NextFloat(lastPrice + offset, lastPrice + offset + (numOrders * askSpread))
        try:
            order = ExecuteOrder("sell", volume, price)
            if order is not None:
                NewOrders.append(order)
        except:
            print("ERROR couldn't execute SELL order. Price:" + str(price) + " volume: " + str(volume))


def ConvertToRadians(angle):
    return (math.pi / 180.0) * angle


def ExecuteMakerBot():
    print("Maker Bot.")
    CancelOrders()
    IssueNewProposal()

stepSize = 0.01
stepMode = 0

def ExecuteTakerBot():
    global stepMode

    print("Taker Bot.")
    minVolume = 0.0001
    MAX_Volume = 1.0
    MAX_SELL_Volume = 1.0
    numTakerBots = 4
    amplitude = 100

    if stepMode >= 360:
        stepMode = 0
        stepSize = NextFloat(0.01, 10.00)

    for k in range(0, numTakerBots):
        marketDepth = client.get_snapshot()
        tickers = client.get_tickers()
        lastPrice = float(tickers[TradingPair]['ticker']['last'])

        if NextFloat(0, 10) >= 2:
            # BUY
            if len(marketDepth['asks']) > 0:
                # Get minimum buying price and max volume.
                minBuyPrice = sys.float_info.max
                maxVolume = minVolume
                found = False
                for i in range(0, len(marketDepth['asks'])):
                    ask = marketDepth['asks'][i]
                    askP = float(ask[0])
                    if askP < minBuyPrice and askP >= lastPrice:
                        minBuyPrice = askP
                        maxVolume = float(ask[1])
                        found = True
                
                volume = NextFloat(minVolume, amplitude * math.sin(ConvertToRadians(stepMode * (maxVolume % MAX_Volume))))
                #volume = NextFloat(minVolume, maxVolume % MAX_Volume)
                price = minBuyPrice
                
                if price >= lastPrice and found:
                    try:
                        order = ExecuteOrder("buy", volume, price)
                    except:
                        print("ERROR couldn't execute BUY order. Price:" + str(price) + " volume: " + str(volume))
        else:
            # SELL
            if len(marketDepth['bids']) > 0:
                # Get maximum selling price and max volume.
                maxSellPrice = sys.float_info.min
                maxVolume = minVolume
                found = False
                for i in range(0, len(marketDepth['bids'])):
                    bid = marketDepth['bids'][i]
                    bidP = float(bid[0])
                    if bidP > maxSellPrice and bidP <= lastPrice:
                        maxSellPrice = bidP
                        maxVolume = float(bid[1])
                        found = True
                
                volume = NextFloat(minVolume, amplitude * math.sin(ConvertToRadians(stepMode * (maxVolume % MAX_SELL_Volume))))
                #volume = NextFloat(minVolume, maxVolume % MAX_SELL_Volume)
                price = maxSellPrice
                
                if price <= lastPrice and found:
                    try:
                        order = ExecuteOrder("sell", volume, price)
                    except:
                        print("ERROR couldn't execute SELL order. Price:" + str(price) + " volume: " + str(volume))                        
# Begin program.
PrintMarkets()
PrintBalances()

ticks = 0
while(True):
    #if (ticks == 0 or ticks % 5 == 0):
    PrintOrders()
    ExecuteMakerBot()
    ExecuteTakerBot()

    time.sleep(10.0)
    ticks += 1