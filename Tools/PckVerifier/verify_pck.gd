extends SceneTree

const SHADER_PATHS := [
	"res://addons/gd_cubism/res/shader/2d_cubism_mask.gdshader",
	"res://addons/gd_cubism/res/shader/2d_cubism_mask_add.gdshader",
	"res://addons/gd_cubism/res/shader/2d_cubism_mask_add_inv.gdshader",
	"res://addons/gd_cubism/res/shader/2d_cubism_mask_mix.gdshader",
	"res://addons/gd_cubism/res/shader/2d_cubism_mask_mix_inv.gdshader",
	"res://addons/gd_cubism/res/shader/2d_cubism_mask_mul.gdshader",
	"res://addons/gd_cubism/res/shader/2d_cubism_mask_mul_inv.gdshader",
	"res://addons/gd_cubism/res/shader/2d_cubism_norm_add.gdshader",
	"res://addons/gd_cubism/res/shader/2d_cubism_norm_mix.gdshader",
	"res://addons/gd_cubism/res/shader/2d_cubism_norm_mul.gdshader",
]

func _initialize() -> void:
	var arguments := OS.get_cmdline_user_args()
	if arguments.is_empty():
		printerr("Live2D.pck path is required.")
		quit(2)
		return

	if not ProjectSettings.load_resource_pack(arguments[0], true):
		printerr("Unable to mount PCK: ", arguments[0])
		quit(3)
		return

	var loaded := 0
	for path in SHADER_PATHS:
		if not ResourceLoader.exists(path):
			printerr("Missing shader in PCK: ", path)
			continue
		if ResourceLoader.load(path, "Shader") == null:
			printerr("Unable to load shader from PCK: ", path)
			continue
		loaded += 1

	print("Cubism shaders verified from isolated PCK: %d/%d" % [loaded, SHADER_PATHS.size()])
	quit(0 if loaded == SHADER_PATHS.size() else 4)
