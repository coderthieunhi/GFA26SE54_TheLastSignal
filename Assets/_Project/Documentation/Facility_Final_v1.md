# TÀI LIỆU THAM CHIẾU KẾT CẤU BẢN ĐỒ CƠ SỞ (FACILITY MAP REFERENCE - V1.2)

> **NGÀY CẬP NHẬT:** 16/09/2026  
> **PHIÊN BẢN:** 1.2 (Official Prototype Final - Phase 1 Expansion)  
> **NGUỒN SỰ THẬT DUY NHẤT (SINGLE SOURCE OF TRUTH):** `Assets/Scenes/SampleScene.unity`  
> **NHẬT KÝ THAY ĐỔI:**
> - Bổ sung phòng `Bedroom` (intro) nối vào cạnh Tây Airlock.
> - **Hoàn tất mở rộng phòng Đợt 1**:
>   - `Reactor_Core`: Mở rộng từ $10 \times 10 \to 12 \times 10$ (`[4, -52, 15, -43]`), phình đối xứng 2 bên Đông - Tây, cập nhật vị trí 2 bồn làm mát.
>   - `Crew_Quarters`: Mở rộng từ $10 \times 8 \to 10 \times 10$ (`[-5, 25, 4, 34]`), dịch về phía Bắc kéo dài hành lang `Corridor_Crew_NodeA` từ 3 ô thành 5 ô, dời cửa Nam lên $Y = 24.5$, cập nhật vị trí bàn và TaskLocation theo tâm mới.
>   - `Bedroom`: Mở rộng từ $8 \times 6 \to 9 \times 8$ (`[-15, 1, -7, 8]`), giữ hành lang 2 ô, vách đôi chạm sát Mess_Hall (đúng 0 ô đệm giữa 2 bờ tường), cập nhật 4 giường intro.

---

## 1. TUYÊN BỐ CHÍNH THỨC (OFFICIAL DECLARATION)

> [!IMPORTANT]
> Kể từ phiên bản này (**16/09/2026 - v1.2**), Scene `SampleScene.unity` là **Single Source of Truth** cho toàn bộ cấu trúc map layout của dự án.
> 
> - **Lưu ý Generator**: Menu `Tools > Facility > Generate Prototype Tilemap` được trang bị hộp thoại cảnh báo xác nhận (`EditorUtility.DisplayDialog`) để tránh vô tình ghi đè.
> - **Điểm khôi phục an toàn (Restore Points)**:
>   - Bản gốc chốt v1.0: `Assets/Scenes/SampleScene_v1_Final.unity`.
>   - Bản chốt sau Đợt 1 v1.2: `Assets/Scenes/SampleScene_v1.2_Final.unity`.

---

## 2. CHỈ SỐ HÌNH HỌC TỔNG THỂ (VERIFIED METRICS V1.2)

- **Tổng số MapBlock:** 33 blocks (11 Rooms, 3 Nodes, 19 Corridors)
- **Tổng số ô sàn (Floor Tiles):** **1,290 ô** *(tăng +120 ô so với bản v1.0)*
- **Tổng số ô tường (Wall Tiles):** **676 ô** *(tăng +46 ô so với bản v1.0)*
- **Tổng số ô bị chiếm dụng (Occupied Tiles):** 89 ô (18 Obstacle blocks + 10 TaskLocations; 4 giường trong Bedroom là cosmetic non-collider)
- **Tổng số ô có thể di chuyển (Walkable Tiles):** **1,201 ô** *(1,290 - 89)*
- **Độ kết nối (Connectivity):** **1,290 / 1,290 ô đạt 100% reachable** qua thuật toán BFS từ điểm xuất phát (`(0, 3)`). Không có bất kỳ ô nào bị cô lập.
- **Tổng số Cửa (Doors):** 13 cửa (12 cửa v1.0 + 1 cửa `Door_Airlock_West`).

---

## 3. BẢNG TRA CỨU TOÀN BỘ 33 MAP BLOCKS

### 3.1. Các Phòng Chính (11 Rooms)

| Tên Phòng | Loại | Bounds [MinX, MinY, MaxX, MaxY] | Kích Thước (W x H) | Diện Tích (ô) | Doorways Kết Nối |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Cargo_Hold** | Room | `[-6, -21, 5, -12]` | 12 x 10 | 120 | Bắc: `X∈[-1,0], Y=-11` (Node B)<br>Nam: `X∈[-1,0], Y=-22` (Node C) |
| **Reactor_Core** *(v1.2)* | Room | `[4, -52, 15, -43]` | 12 x 10 | 120 | Bắc: `X∈[9,10], Y=-42` (nối Corridor_Relay_Reactor) |
| **Crew_Quarters** *(v1.2)* | Room | `[-5, 25, 4, 34]` | 10 x 10 | 100 | Nam: `X∈[-1,0], Y=24` (nối Corridor_Crew_NodeA) |
| **Console_Alpha** | Room | `[6, 13, 15, 20]` | 10 x 8 | 80 | Tây: `X=6, Y∈[16,17]` (nối Corridor_NodeA_Console) |
| **Deep_Storage** | Room | `[-15, -39, -6, -32]` | 10 x 8 | 80 | Đông: `X=-6, Y∈[-36,-35]` (Node C West Branch)<br>Tây: `X=-16, Y∈[-34,-33]` (Loop West) |
| **Power_Relay_Bay** | Room | `[5, -39, 14, -32]` | 10 x 8 | 80 | Tây: `X=5, Y∈[-36,-35]` (Node C East Branch)<br>Nam: `X∈[9,10], Y=-40` (Reactor Core) |
| **Vent_Shaft** | Room | `[6, -10, 13, -1]` | 8 x 10 | 80 | Tây lệch: `X=6, Y∈[-4,-3]` (Node B)<br>Đông: `X=14, Y∈[-10,-9]` (Loop East) |
| **Bedroom** *(v1.2 - Intro)* | Room | `[-15, 1, -7, 8]` | 9 x 8 | 72 | Đông: `X=-7, Y∈[4,5]` (nối Corridor_Bedroom_Airlock) |
| **Airlock_Spawn** | Room | `[-4, 1, 3, 8]` | 8 x 8 | 64 | Bắc: `X∈[-1,0], Y=9` (Node A)<br>Nam: `X∈[-1,0], Y=0` (Node B)<br>Tây: `X=-4, Y∈[4,5]` (Bedroom) |
| **Mess_Hall** | Room | `[-14, -9, -7, -2]` | 8 x 8 | 64 | Đông lệch: `X=-7, Y∈[-4,-3]` (nối Corridor_MessHall_NodeB) |
| **Med_Bay** | Room | `[-14, 14, -7, 19]` | 8 x 6 | 48 | Đông: `X=-7, Y∈[16,17]` (Node A)<br>Tây: `X=-15, Y∈[14,15]` (Loop West) |

### 3.2. Các Nút Giao Thông (3 Nodes)

| Tên Node | Loại | Bounds [MinX, MinY, MaxX, MaxY] | Kích Thước (W x H) | Diện Tích (ô) | Ghi Chú Cấu Trúc |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Node_A** | Node | `[-3, 14, 2, 19]` | 6 x 6 | 36 | Ngã 4: Crew Quarters (Bắc), Airlock (Nam), Med Bay (Tây), Console Alpha (Đông) |
| **Node_B** | Node | `[-3, -9, 2, -2]` | 6 x 8 | 48 | Hub trung tâm: Airlock (Bắc), Cargo Hold (Nam), Mess Hall (Tây), Vent Shaft (Đông) |
| **Node_C** | Node | `[-3, -29, 2, -25]` | 6 x 5 | 30 | Giao lộ phía Nam: Cargo Hold (Bắc), Stem phân nhánh Nam (Nam), Loop East Bend (Đông) |

### 3.3. Các Hành Lang Trục Chính & Nhánh Phụ (19 Corridors)

| Tên Hành Lang | Bounds [MinX, MinY, MaxX, MaxY] | Kích Thước | Chức Năng Kết Nối |
| :--- | :--- | :--- | :--- |
| **Corridor_Crew_NodeA** *(v1.2)*| `[-1, 20, 0, 24]` | 2 x 5 | Trục chính kéo dài: Node A ↔ Crew Quarters (5 ô) |
| **Corridor_NodeA_Airlock** | `[-1, 9, 0, 13]` | 2 x 5 | Trục chính: Node A ↔ Airlock Spawn |
| **Corridor_Airlock_NodeB** | `[-1, -1, 0, 0]` | 2 x 2 | Trục chính: Airlock Spawn ↔ Node B |
| **Corridor_NodeB_Cargo** | `[-1, -11, 0, -10]` | 2 x 2 | Trục chính: Node B ↔ Cargo Hold |
| **Corridor_Cargo_NodeC** | `[-1, -24, 0, -22]` | 2 x 3 | Trục chính: Cargo Hold ↔ Node C |
| **Corridor_MedBay_NodeA** | `[-6, 16, -4, 17]` | 3 x 2 | Nhánh ngang Tây: Node A ↔ Med Bay |
| **Corridor_NodeA_Console** | `[3, 16, 5, 17]` | 3 x 2 | Nhánh ngang Đông: Node A ↔ Console Alpha |
| **Corridor_MessHall_NodeB** | `[-6, -4, -4, -3]` | 3 x 2 | Nhánh ngang Tây lệch: Node B ↔ Mess Hall |
| **Corridor_NodeB_VentShaft** | `[3, -4, 5, -3]` | 3 x 2 | Nhánh ngang Đông lệch: Node B ↔ Vent Shaft |
| **Corridor_C_Stem** | `[-1, -36, 0, -30]` | 2 x 7 | Cuống trục Nam: Node C ↔ Ngã 3 T-Junction |
| **Corridor_C_Branch_West** | `[-5, -36, -2, -35]` | 4 x 2 | Nhánh T phía Tây: C Stem ↔ Deep Storage |
| **Corridor_C_Branch_East** | `[1, -36, 4, -35]` | 4 x 2 | Nhánh T phía Đông: C Stem ↔ Power Relay Bay |
| **Corridor_Relay_Reactor** | `[9, -42, 10, -40]` | 2 x 3 | Trục dọc Nam: Power Relay Bay ↔ Reactor Core |
| **Loop_Vertical** | `[14, -28, 15, -10]` | 2 x 19 | Vòng lặp Đông (dọc): Vent Shaft South ↔ C Bend |
| **Loop_Horizontal_Bend** | `[3, -28, 14, -27]` | 12 x 2 | Vòng lặp Đông (ngang): Node C East ↔ Loop Vertical |
| **Loop_West_Vertical** | `[-19, -34, -18, 15]` | 2 x 50 | Vòng lặp Tây (dọc chạy dọc biên X = -19, -18) |
| **Loop_West_Bend_North** | `[-17, 14, -15, 15]` | 3 x 2 | Cua bẻ Tây Bắc: Loop West Vertical ↔ Med Bay |
| **Loop_West_Bend_South** | `[-17, -34, -16, -33]`| 2 x 2 | Cua bẻ Tây Nam: Loop West Vertical ↔ Deep Storage |
| **Corridor_Bedroom_Airlock** | `[-6, 4, -5, 5]` | 2 x 2 | Nhánh ngang Tây: Bedroom ↔ Airlock Spawn |

---

## 4. DANH SÁCH CỬA (13 DOORS)

1. **`Door_Crew_NodeA`** *(v1.2)* (Ngang): `(0.0, 24.5, 0.0)`
2. `Door_NodeA_Airlock` (Ngang): `(0.0, 9.5, 0.0)`
3. `Door_Airlock_South` (Ngang): `(0.0, 0.5, 0.0)`
4. `Door_NodeB_Cargo` (Ngang): `(0.0, -10.5, 0.0)`
5. `Door_Cargo_NodeC` (Ngang): `(0.0, -21.5, 0.0)`
6. `Door_MedBay_NodeA` (Dọc): `(-5.5, 17.0, 0.0)`
7. `Door_ConsoleAlpha` (Dọc): `(5.5, 17.0, 0.0)`
8. `Door_MessHall_NodeB` (Dọc): `(-5.5, -3.0, 0.0)`
9. `Door_VentShaft` (Dọc): `(5.5, -3.0, 0.0)`
10. `Door_DeepStorage` (Dọc): `(-4.5, -35.0, 0.0)`
11. `Door_PowerRelayBay` (Dọc): `(4.5, -35.0, 0.0)`
12. `Door_ReactorCore` (Ngang): `(10.0, -41.5, 0.0)`
13. `Door_Airlock_West` (Dọc): `(-4.5, 5.0, 0.0)`

---

## 5. VỊ TRÍ ĐỐI TƯỢNG ĐÃ ĐIỀU CHỈNH THEO TÂM MỚI (V1.2)

### 5.1. Crew_Quarters
- `TaskLoc_CrewQuarters`: `(-0.5, 29.5, 0.0)`
- `Obstacle_CrewQuarters_Table`: `(-3.0, 29.5, 0.0)`

### 5.2. Reactor_Core
- `TaskLoc_ReactorCore`: `(9.5, -47.5, 0.0)`
- `Obstacle_ReactorCore_BasePipe_South`: `(9.5, -51.5, 0.0)`
- `Obstacle_ReactorCore_CoolantTank_West`: `(5.0, -47.5, 0.0)`
- `Obstacle_ReactorCore_CoolantTank_East`: `(14.5, -47.5, 0.0)`

### 5.3. Bedroom (4 Intro Beds)
- `Bed_P1` (Tây Bắc): `(-13.5, 7.0, 0.0)`
- `Bed_P2` (Đông Bắc): `(-8.5, 7.0, 0.0)`
- `Bed_P3` (Tây Nam): `(-13.5, 2.0, 0.0)`
- `Bed_P4` (Đông Nam): `(-8.5, 2.0, 0.0)`
- **Khoảng đệm với Mess_Hall**: Đúng 0 ô đệm giữa 2 bức tường bao (vách đôi kín khít).
