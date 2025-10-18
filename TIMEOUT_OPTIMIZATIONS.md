# HKMP Timeout and Performance Optimizations

## Summary
This document details all timeout and performance optimizations implemented to reduce lag and improve gameplay experience in the Hollow Knight Multiplayer mod.

## Optimizations Applied

### 1. Connection Timeout Optimizations

#### ConnectionManager.cs
- **Previous**: 60000ms (60 seconds)
- **Optimized**: 15000ms (15 seconds)
- **Impact**: Faster failure detection for failed connections, reducing player wait time by 75%

### 2. Heartbeat/Connection Timeout

#### UdpUpdateManager.cs
- **Previous**: 5000ms (5 seconds) - Fixed timeout
- **Optimized**: 10000ms base with adaptive timeout
- **New Features**:
  - Adaptive timeout based on RTT (Round Trip Time)
  - Dynamic adjustment between 3000ms - 20000ms depending on connection quality
  - RTT-based multipliers:
    - < 50ms RTT: 0.5x multiplier (low-latency connections)
    - 50-100ms RTT: 0.75x multiplier
    - 100-200ms RTT: 1.0x multiplier
    - 200-400ms RTT: 1.5x multiplier
    - > 400ms RTT: 2.0x multiplier (high-latency connections)
- **Impact**: Prevents premature disconnections on high-latency connections while maintaining fast detection on low-latency connections

### 3. DTLS Handshake Timeout

#### DtlsClient.cs
- **Previous**: 5000ms (5 seconds)
- **Optimized**: 8000ms (8 seconds)
- **Impact**: Better support for high-latency connections during initial handshake

#### DTLS Receive Loop
- **Previous**: 5ms timeout per receive call
- **Optimized**: 1ms timeout per receive call
- **Impact**: More responsive packet reception, reduced latency

### 4. Chunk Transmission Optimizations

#### ChunkSender.cs
- **Slice Send Interval**:
  - **Previous**: 20ms between slices
  - **Optimized**: 10ms between slices
  - **Impact**: 2x faster chunk transmission speed

- **Slice Resend Timeout**:
  - **Previous**: 100ms before resending
  - **Optimized**: 50ms before resending
  - **Impact**: Faster recovery from packet loss, 2x faster retry

### 5. Connection Pool Optimizations

#### ConnectionPool.cs
- **Acquire Timeout**:
  - **Previous**: 30 seconds
  - **Optimized**: 10 seconds
  - **Impact**: Faster timeout for pool operations

- **Retry Delay**:
  - **Previous**: 100ms wait when pool is full
  - **Optimized**: 50ms wait when pool is full
  - **Impact**: 2x faster retry rate when pool is saturated

### 6. Network Message Processing

#### OptimizedNetworkManager.cs
- **Send Batch Size**:
  - **Previous**: 50 messages per batch
  - **Optimized**: 100 messages per batch
  - **Impact**: Better batching efficiency, reduced overhead

- **Processing Intervals**:
  - **Previous**: 10ms send interval, 10ms receive interval
  - **Optimized**: 5ms send interval, 5ms receive interval
  - **Impact**: 2x faster message processing throughput

### 7. Server Throttle Time

#### NetServer.cs
- **Previous**: 2500ms throttle for rejected connections
- **Optimized**: 1500ms throttle for rejected connections
- **Impact**: Faster reconnection attempts after rejection (40% faster)

## Performance Benefits

1. **Connection Speed**: Up to 75% faster connection failure detection
2. **Chunk Transfer**: 2x faster data chunk transmission
3. **Packet Loss Recovery**: 2x faster retry on packet loss
4. **Network Responsiveness**: 2x faster message processing
5. **Adaptive Behavior**: Automatic adjustment to connection quality prevents premature timeouts
6. **Reduced Lag**: Lower processing intervals reduce input-to-network latency

## Adaptive Timeout Algorithm

The adaptive timeout system adjusts based on measured RTT:

```
Adaptive Timeout = max(BaseTimeout × Multiplier, RTT × 4, 3000ms)

Where Multiplier is:
- 0.5  for RTT < 50ms
- 0.75 for RTT 50-100ms
- 1.0  for RTT 100-200ms
- 1.5  for RTT 200-400ms
- 2.0  for RTT > 400ms
```

This ensures:
- Fast timeout detection on LAN/low-latency connections
- Stable connections on high-latency/international connections
- Minimum 3-second safety threshold to prevent false positives

## Recommendations for Server Operators

1. **Low-Latency Networks (LAN, Local)**: No configuration needed, benefits from aggressive timeouts
2. **Medium-Latency Networks (Regional, < 100ms)**: Default settings optimized for this scenario
3. **High-Latency Networks (International, > 200ms)**: System automatically adjusts, but consider:
   - Monitoring connection quality
   - Informing players about expected latency

## Testing Recommendations

1. Test on various network conditions:
   - LAN (< 10ms RTT)
   - Regional (50-100ms RTT)
   - International (> 200ms RTT)

2. Test scenarios:
   - Normal gameplay
   - Packet loss simulation (1-5%)
   - High-latency connections
   - Connection interruptions

3. Monitor:
   - Connection stability
   - Timeout frequency
   - Reconnection success rate
   - Player experience

## Future Optimization Opportunities

1. **Dynamic MTU Discovery**: Automatically detect optimal packet size
2. **Predictive Loss Detection**: Use statistical models to predict packet loss
3. **Quality of Service (QoS)**: Prioritize critical game state packets
4. **Connection Migration**: Seamless handover between network interfaces
5. **Bandwidth Estimation**: Adaptive send rate based on available bandwidth
