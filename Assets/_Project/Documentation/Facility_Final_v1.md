# TÀI LIỆU THAM CHIẾU KẾT CẤU BẢN ĐỒ CƠ SỞ (FACILITY MAP REFERENCE - V1 FINAL)

> **NGÀY CHỐT CHÍNH THỨC:** 14/09/2026  
> **PHIÊN BẢN:** 1.0 (Official Prototype Final)  
> **NGUỒN SỰ THẬT DUY NHẤT (SINGLE SOURCE OF TRUTH):** `Assets/Scenes/SampleScene.unity`

---

## 1. TUYÊN BỐ CHÍNH THỨC (OFFICIAL DECLARATION)

> [!IMPORTANT]
> Kể từ thời điểm này (**14/09/2026**), Scene `SampleScene.unity` là **Single Source of Truth** cho toàn bộ cấu trúc map layout của dự án.
> 
> - **KHÔNG** sử dụng menu `Tools > Facility > Generate Prototype Tilemap` để tạo lại bản đồ trong quá trình phát triển thông thường. Menu này đã được khóa bằng hộp thoại cảnh báo xác nhận (`EditorUtility.DisplayDialog`) để tránh vô tình ghi đè.
> - **Mọi chỉnh sửa bản đồ tiếp theo** (thêm/sửa/xóa tile, đặt chướng ngại vật, cửa, task markers, trigger zones) phải được thực hiện **trực tiếp trên Scene** `SampleScene.unity` hoặc thông qua prefab/workflow chuyên biệt.
> - Bản sao lưu phục hồi an toàn (Restore Point) được lưu trữ tại: `Assets/Scenes/SampleScene_v1_Final.unity`.

---

## 2. CHỈ SỐ HÌNH HỌC TỔNG THỂ (VERIFIED METRICS)

- **Tổng số MapBlock:** 31 blocks (10 Rooms, 3 Nodes, 18 Corridors)
- **Tổng số ô sàn (Floor Tiles):** 1,170 ô
- **Tổng số ô tường (Wall Tiles):** 630 ô
- **Tổng số ô bị chiếm dụng (Occupied Tiles):** 89 ô (18 Obstacle blocks + 10 TaskLocations)
- **Tổng số ô có thể di chuyển (Walkable Tiles):** 1,081 ô
- **Độ kết nối (Connectivity):** 1,081 / 1,081 ô đạt 100% reachable qua thuật toán BFS từ điểm xuất phát (`Spawn_Airlock` tại tọa độ `(0, 3)`).
- **Tổng số Cửa (Doors):** 12 cửa (hoạt động độc lập, tự động đóng/mở có collider).

---

## 3. BẢNG TRA CỨU TOÀN BỘ 31 MAP BLOCKS

### 3.1. Các Phòng Chính (10 Rooms)

| Tên Phòng | Loại | Bounds [MinX, MinY, MaxX, MaxY] | Kích Thước (W x H) | Diện Tích (ô) | Doorways Kết Nối |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Crew_Quarters** | Room | `[-5, 23, 4, 30]` | 10 x 8 | 80 | Nam: `X∈[-1,0], Y=22` (nối Corridor_Crew_NodeA) |
| **Med_Bay** | Room | `[-14, 14, -7, 19]` | 8 x 6 | 48 | Đông: `X=-7, Y∈[16,17]` (Node A)<br>Tây: `X=-15, Y∈[14,15]` (Loop West) |
| **Console_Alpha** | Room | `[6, 13, 15, 20]` | 10 x 8 | 80 | Tây: `X=6, Y∈[16,17]` (nối Corridor_NodeA_Console) |
| **Airlock_Spawn** | Room | `[-4, 1, 3, 8]` | 8 x 8 | 64 | Bắc: `X∈[-1,0], Y=9` (Node A)<br>Nam: `X∈[-1,0], Y=0` (Node B) |
| **Mess_Hall** | Room | `[-14, -9, -7, -2]` | 8 x 8 | 64 | Đông lệch: `X=-7, Y∈[-4,-3]` (nối Corridor_MessHall_NodeB) |
| **Vent_Shaft** | Room | `[6, -10, 13, -1]` | 8 x 10 | 80 | Tây lệch: `X=6, Y∈[-4,-3]` (Node B)<br>Đông: `X=14, Y∈[-10,-9]` (Loop East) |
| **Cargo_Hold** | Room | `[-6, -21, 5, -12]` | 12 x 10 | 120 | Bắc: `X∈[-1,0], Y=-11` (Node B)<br>Nam: `X∈[-1,0], Y=-22` (Node C) |
| **Deep_Storage** | Room | `[-15, -39, -6, -32]` | 10 x 8 | 80 | Đông: `X=-6, Y∈[-36,-35]` (Node C West Branch)<br>Tây: `X=-16, Y∈[-34,-33]` (Loop West) |
| **Power_Relay_Bay** | Room | `[5, -39, 14, -32]` | 10 x 8 | 80 | Tây: `X=5, Y∈[-36,-35]` (Node C East Branch)<br>Nam: `X∈[9,10], Y=-40` (Reactor Core) |
| **Reactor_Core** | Room | `[5, -52, 14, -43]` | 10 x 10 | 100 | Bắc: `X∈[9,10], Y=-42` (nối Corridor_Relay_Reactor) |

### 3.2. Các Nút Giao Thông (3 Nodes)

| Tên Node | Loại | Bounds [MinX, MinY, MaxX, MaxY] | Kích Thước (W x H) | Diện Tích (ô) | Ghi Chú Cấu Trúc |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Node_A** | Node | `[-3, 14, 2, 19]` | 6 x 6 | 36 | Ngã 4: Crew Quarters (Bắc), Airlock (Nam), Med Bay (Tây), Console Alpha (Đông) |
| **Node_B** | Node | `[-3, -9, 2, -2]` | 6 x 8 | 48 | Hub trung tâm (mở rộng 6x8): Airlock (Bắc), Cargo Hold (Nam), Mess Hall (Tây), Vent Shaft (Đông) |
| **Node_C** | Node | `[-3, -29, 2, -25]` | 6 x 5 | 30 | Giao lộ phía Nam: Cargo Hold (Bắc), Stem phân nhánh Nam (Nam), Loop East Bend (Đông) |

### 3.3. Các Hành Lang Trục Chính & Nhánh Phụ (18 Corridors)

| Tên Hành Lang | Bounds [MinX, MinY, MaxX, MaxY] | Kích Thước | Chức Năng Kết Nối |
| :--- | :--- | :--- | :--- |
| **Corridor_Crew_NodeA** | `[-1, 20, 0, 22]` | 2 x 3 | Trục chính: Node A ↔ Crew Quarters |
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

---

## 4. DANH SÁCH CỬA (12 DOORS)

1. `Door_Crew_NodeA` (Ngang): `(0.0, 20.5, 0.0)`
2. `Door_NodeA_Airlock` (Ngang): `(0.0, 13.5, 0.0)`
3. `Door_Airlock_NodeB` (Ngang): `(0.0, 0.5, 0.0)`
4. `Door_NodeB_Cargo` (Ngang): `(0.0, -10.5, 0.0)`
5. `Door_Cargo_NodeC` (Ngang): `(0.0, -22.5, 0.0)`
6. `Door_MedBay_NodeA` (Dọc): `(-3.5, 17.0, 0.0)`
7. `Door_NodeA_Console` (Dọc): `(3.5, 17.0, 0.0)`
8. `Door_MessHall_NodeB` (Dọc): `(-3.5, -3.0, 0.0)`
9. `Door_NodeB_VentShaft` (Dọc): `(3.5, -3.0, 0.0)`
10. `Door_C_Branch_West` (Dọc): `(-2.5, -35.0, 0.0)`
11. `Door_C_Branch_East` (Dọc): `(1.5, -35.0, 0.0)`
12. `Door_Relay_Reactor` (Ngang): `(10.0, -40.5, 0.0)`

---

## 5. CÁC TÍNH CHẤT ĐẶC TRƯNG CỦA LAYOUT V1

1. **Topology 2 Vòng Lặp Cân Bằng (Dual Loops):**
   - **Vòng lặp nhánh Đông:** Vent Shaft ↔ Loop Vertical ↔ Loop Horizontal Bend ↔ Node C.
   - **Vòng lặp nhánh Tây:** Med Bay ↔ Loop West Bend North ↔ Loop West Vertical ↔ Loop West Bend South ↔ Deep Storage.
2. **Cửa Lệch Tâm (Asymmetric Ingress):**
   - Lối vào Mess Hall và Vent Shaft được bố trí lệch lên phía Bắc (`Y ∈ [-4, -3]`), tạo góc khuất chiến thuật cho người chơi khi bước vào phòng.
3. **Hub Giao Thông Node B (6 x 8):**
   - Node B có kích thước 48 ô sàn, đủ rộng cho các sự kiện tụ họp, giao tranh hoặc tương tác nhóm, nhưng vẫn giữ nguyên tắc phân cấp diện tích nhỏ hơn các phòng chức năng.

