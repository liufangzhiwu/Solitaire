import os
import re
import sys

def rename_images_in_folder(folder_path):
    """
    重命名指定文件夹中的所有图片文件
    格式：旧名___新名 -> 新名
    """
    # 支持的图片扩展名
    image_extensions = ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.tiff', '.webp', '.svg']
    
    # 获取文件夹中的所有文件
    try:
        files = os.listdir(folder_path)
    except FileNotFoundError:
        print(f"错误：找不到文件夹 '{folder_path}'")
        return
    except PermissionError:
        print(f"错误：没有权限访问文件夹 '{folder_path}'")
        return
    
    renamed_count = 0
    error_count = 0
    
    for filename in files:
        # 检查是否为图片文件
        ext = os.path.splitext(filename)[1].lower()
        if ext not in image_extensions:
            continue
        
        # 分割文件名
        if '___' in filename:
            # 按照最后一个'___'分割，获取新名字
            parts = filename.split('___')
            new_name = parts[-1]  # 取最后一部分
            
            # 构建完整的文件路径
            old_path = os.path.join(folder_path, filename)
            new_path = os.path.join(folder_path, new_name)
            
            # 检查新文件名是否已存在
            counter = 1
            original_new_name = new_name
            while os.path.exists(new_path):
                name_without_ext, ext = os.path.splitext(original_new_name)
                new_name = f"{name_without_ext}_{counter}{ext}"
                new_path = os.path.join(folder_path, new_name)
                counter += 1
            
            try:
                os.rename(old_path, new_path)
                renamed_count += 1
                print(f"✓ 已重命名: {filename} -> {new_name}")
            except Exception as e:
                error_count += 1
                print(f"✗ 重命名失败 {filename}: {e}")
    
    print(f"\n{'='*50}")
    print(f"完成！成功重命名 {renamed_count} 个文件")
    if error_count > 0:
        print(f"失败 {error_count} 个文件")
    print(f"{'='*50}")

if __name__ == "__main__":
    # 使用方法
    if len(sys.argv) > 1:
        folder_path = sys.argv[1]
    else:
        # 如果没有提供文件夹路径，使用当前文件夹
        folder_path = input("请输入图片文件夹路径（直接按回车使用当前文件夹）: ").strip()
        if not folder_path:
            folder_path = "."
    
    # 确认操作
    print(f"将要处理文件夹: {os.path.abspath(folder_path)}")
    confirm = input("确定要执行重命名操作吗？(y/n): ").lower()
    
    if confirm == 'y':
        rename_images_in_folder(folder_path)
    else:
        print("操作已取消")